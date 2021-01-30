using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Verse;
using Verse.Sound;


namespace Verse.AI{
public class JobDriver_Equip : JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return pawn.Reserve(job.targetA, job, errorOnFailed: errorOnFailed);
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDestroyedOrNull(TargetIndex.A);
		this.FailOnBurningImmobile(TargetIndex.A);

		//Goto equipment
		{
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
				.FailOnDespawnedNullOrForbidden(TargetIndex.A);
		}
		
		//Take equipment
		{
			Toil takeEquipment = new Toil();
			takeEquipment.initAction = ()=>
			{
				ThingWithComps eq = ((ThingWithComps)job.targetA.Thing);
				ThingWithComps toEquip = null;

				if( eq.def.stackLimit > 1 && eq.stackCount > 1 )
					toEquip = (ThingWithComps)eq.SplitOff(1);
				else
				{
					toEquip = eq;
					toEquip.DeSpawn();
				}

				pawn.equipment.MakeRoomFor(toEquip);
				pawn.equipment.AddEquipment(toEquip);
		
				if( eq.def.soundInteract != null )
					eq.def.soundInteract.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
			};
			takeEquipment.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return takeEquipment;
		}
	}
}}











