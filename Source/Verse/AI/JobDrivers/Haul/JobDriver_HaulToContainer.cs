using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;


namespace Verse.AI{
public class JobDriver_HaulToContainer : JobDriver
{
	//Constants
	protected const TargetIndex CarryThingIndex = TargetIndex.A;
	protected const TargetIndex DestIndex = TargetIndex.B;
	protected const TargetIndex PrimaryDestIndex = TargetIndex.C;

	public Thing ThingToCarry { get { return (Thing)job.GetTarget(CarryThingIndex); } }
	public Thing Container { get { return (Thing)job.GetTarget(DestIndex); } }
	private int Duration { get { return Container != null && Container is Building ? Container.def.building.haulToContainerDuration : 0; } }

    public override string GetReport()
	{
		Thing hauledThing = null;
		if( pawn.CurJob == job && pawn.carryTracker.CarriedThing != null )
			hauledThing = pawn.carryTracker.CarriedThing;
		else
			hauledThing = TargetThingA;

		if( hauledThing == null || !job.targetB.HasThing )
			return "ReportHaulingUnknown".Translate();
		else
			return "ReportHaulingTo".Translate(hauledThing.Label, job.targetB.Thing.LabelShort.Named("DESTINATION"), hauledThing.Named("THING"));
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		if( !pawn.Reserve(job.GetTarget(CarryThingIndex), job, errorOnFailed: errorOnFailed) )
			return false;
			
		if( !pawn.Reserve(job.GetTarget(DestIndex), job, errorOnFailed: errorOnFailed) )
			return false;

		pawn.ReserveAsManyAsPossible(job.GetTargetQueue(CarryThingIndex), job);
		pawn.ReserveAsManyAsPossible(job.GetTargetQueue(DestIndex), job);

		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDestroyedOrNull( CarryThingIndex );
		this.FailOnDestroyedNullOrForbidden( DestIndex );
		this.FailOn(() => TransporterUtility.WasLoadingCanceled(Container));
		this.FailOn(() =>
			{
				var thingOwner = Container.TryGetInnerInteractableThingOwner();
				if( thingOwner != null && !thingOwner.CanAcceptAnyOf(ThingToCarry) )
					return true;

				// e.g. grave
				var haulDestination = Container as IHaulDestination;
				if( haulDestination != null && !haulDestination.Accepts(ThingToCarry) )
					return true;

				return false;
			});

		var getToHaulTarget = Toils_Goto.GotoThing( CarryThingIndex, PathEndMode.ClosestTouch )
			.FailOnSomeonePhysicallyInteracting(CarryThingIndex);
		yield return getToHaulTarget;

		yield return Toils_Construct.UninstallIfMinifiable(CarryThingIndex)
			.FailOnSomeonePhysicallyInteracting(CarryThingIndex);

		yield return Toils_Haul.StartCarryThing(CarryThingIndex, subtractNumTakenFromJobCount: true);

		yield return Toils_Haul.JumpIfAlsoCollectingNextTargetInQueue( getToHaulTarget, CarryThingIndex );

		Toil carryToContainer = Toils_Haul.CarryHauledThingToContainer();
		yield return carryToContainer;

		yield return Toils_Goto.MoveOffTargetBlueprint(DestIndex);
		
		//Prepare
		{
			var prepare = Toils_General.Wait(Duration, face: DestIndex);
			prepare.WithProgressBarToilDelay(DestIndex);
			yield return prepare;
		}
		
		yield return Toils_Construct.MakeSolidThingFromBlueprintIfNecessary(DestIndex, PrimaryDestIndex);
		
		yield return Toils_Haul.DepositHauledThingInContainer(DestIndex, PrimaryDestIndex);
		
		yield return Toils_Haul.JumpToCarryToNextContainerIfPossible(carryToContainer, PrimaryDestIndex);
	}
}}

