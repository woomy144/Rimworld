using UnityEngine;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;


namespace Verse.AI{
public class JobDriver_Goto : JobDriver
{
	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		pawn.Map.pawnDestinationReservationManager.Reserve( pawn, job, job.targetA.Cell );

		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		{
			var gotoCell = Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.OnCell);
			gotoCell.AddPreTickAction(() =>
				{
					// we check exit grid every tick to make sure the pawn leaves the map as soon as possible
					if( job.exitMapOnArrival && pawn.Map.exitMapGrid.IsExitCell(pawn.Position) )
						TryExitMap();
				});

			// only allowed to join or create caravan?
			gotoCell.FailOn(() => job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(pawn));

			yield return gotoCell;
		}

		{
			Toil arrive = new Toil();
			arrive.initAction = () =>
				{
					// check if we arrived to our forced goto position
					if( pawn.mindState != null && pawn.mindState.forcedGotoPosition == TargetA.Cell )
						pawn.mindState.forcedGotoPosition = IntVec3.Invalid;

					if( job.exitMapOnArrival && (pawn.Position.OnEdge(pawn.Map) || pawn.Map.exitMapGrid.IsExitCell(pawn.Position)) )
						TryExitMap();
				};

			arrive.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return arrive;
		}
	}

	private void TryExitMap()
	{
		// only allowed to join or create caravan?
		if( job.failIfCantJoinOrCreateCaravan && !CaravanExitMapUtility.CanExitMapAndJoinOrCreateCaravanNow(pawn) )
			return;

		pawn.ExitMap(true, CellRect.WholeMap(Map).GetClosestEdge(pawn.Position));
	}
}}

