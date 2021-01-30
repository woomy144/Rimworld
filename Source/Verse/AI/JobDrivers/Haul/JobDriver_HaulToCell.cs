using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using Verse;
using RimWorld;

namespace Verse.AI
{

public class JobDriver_HaulToCell : JobDriver
{
	//Working vars
	private bool forbiddenInitially;

	//Constants
	private const TargetIndex HaulableInd = TargetIndex.A;
	private const TargetIndex StoreCellInd = TargetIndex.B;

	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref forbiddenInitially, "forbiddenInitially");
	}

	public override string GetReport()
	{
		var destLoc = job.targetB.Cell;

		Thing hauledThing = null;
		if( pawn.CurJob == job && pawn.carryTracker.CarriedThing != null )
			hauledThing = pawn.carryTracker.CarriedThing;
		else if( TargetThingA != null && TargetThingA.Spawned )
			hauledThing = TargetThingA;

		if( hauledThing == null )
			return "ReportHaulingUnknown".Translate();

		string destName = null;
		var destGroup = destLoc.GetSlotGroup(Map);
		if( destGroup != null )
			destName = destGroup.parent.SlotYielderLabel();

		if( destName != null )
			return "ReportHaulingTo".Translate(hauledThing.Label, destName.Named("DESTINATION"), hauledThing.Named("THING"));
		else
			return "ReportHauling".Translate(hauledThing.Label, hauledThing);
	}
	
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return pawn.Reserve(job.GetTarget(StoreCellInd), job, errorOnFailed: errorOnFailed)
			&& pawn.Reserve(job.GetTarget(HaulableInd), job, errorOnFailed: errorOnFailed);
	}

	public override void Notify_Starting()
	{
		base.Notify_Starting();

		if( TargetThingA != null )
			forbiddenInitially = TargetThingA.IsForbidden(pawn);
		else
			forbiddenInitially = false;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		//Set fail conditions
		this.FailOnDestroyedOrNull( HaulableInd );
		this.FailOnBurningImmobile( StoreCellInd );

		//Note we only fail on forbidden if the target doesn't start that way
		//This helps haul-aside jobs on forbidden items
		//
		// TODO instead of this, just use Job.ignoreForbidden where appropriate
		//
		if( !forbiddenInitially )
			this.FailOnForbidden( HaulableInd );

		//Reserve thing to be stored
		//This is redundant relative to MakePreToilReservations(), but the redundancy doesn't hurt, and if we end up looping and grabbing more things, it's necessary
		var reserveTargetA = Toils_Reserve.Reserve( HaulableInd );
		yield return reserveTargetA;

		Toil toilGoto = null;
		toilGoto = Toils_Goto.GotoThing( HaulableInd, PathEndMode.ClosestTouch )
			.FailOnSomeonePhysicallyInteracting(HaulableInd)
			.FailOn( ()=>
			{
				//Note we don't fail on losing hauling designation
				//Because that's a special case anyway

				//While hauling to cell storage, ensure storage dest is still valid
				Pawn actor = toilGoto.actor;
				Job curJob = actor.jobs.curJob;
				if( curJob.haulMode == HaulMode.ToCellStorage )
				{
					Thing haulThing = curJob.GetTarget( HaulableInd ).Thing;

					IntVec3 destLoc = actor.jobs.curJob.GetTarget(TargetIndex.B).Cell;
					if(!destLoc.IsValidStorageFor(Map, haulThing)  )
						return true;
				}

				return false;
			});
		yield return toilGoto;


		yield return Toils_Haul.StartCarryThing( HaulableInd, subtractNumTakenFromJobCount: true );

		if( job.haulOpportunisticDuplicates )
			yield return Toils_Haul.CheckForGetOpportunityDuplicate( reserveTargetA, HaulableInd, StoreCellInd );

		Toil carryToCell = Toils_Haul.CarryHauledThingToCell( StoreCellInd );
		yield return carryToCell;

		yield return Toils_Haul.PlaceHauledThingInCell(StoreCellInd, carryToCell, true);
	}
}

}
