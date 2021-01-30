using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace Verse.AI
{

public class JobDriver_Wait : JobDriver
{
	//Constants
	private const int TargetSearchInterval = 4;

	public override string GetReport()
	{
		if( job.def == JobDefOf.Wait_Combat )
		{
			// special case for when the pawn is incapable of violence,
			// so we don't say he's watching for targets
			if( pawn.RaceProps.Humanlike && pawn.story.WorkTagIsDisabled(WorkTags.Violent) )
				return "ReportStanding".Translate();
			else
				return base.GetReport();
		}

		return base.GetReport();
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		// We don't reserve anything here because the reservation target is "the pawn's current cell", which we can't predict until we get there
		// And which, in theory, will already be reserved

		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		Toil wait = new Toil();
		wait.initAction = ()=>
		{
			Map.pawnDestinationReservationManager.Reserve(pawn, job, pawn.Position);
			pawn.pather.StopDead();

			//On init to stop 1-2 frame flickers out of cooldown/warmup stances
			CheckForAutoAttack(); 
		};
		wait.tickAction = ()=>
		{
			//Bug catcher
			if( job.expiryInterval == -1 && job.def == JobDefOf.Wait_Combat && !pawn.Drafted )
			{
				Log.Error(pawn + " in eternal WaitCombat without being drafted.");
				ReadyForNextToil();
				return;
			}

			if( (Find.TickManager.TicksGame + pawn.thingIDNumber) % TargetSearchInterval == 0 )
				CheckForAutoAttack();
		};
		DecorateWaitToil(wait);
		wait.defaultCompleteMode = ToilCompleteMode.Never;
		yield return wait;
	}

	public virtual void DecorateWaitToil(Toil wait)
	{

	}

	public override void Notify_StanceChanged()
	{
		if( pawn.stances.curStance is Stance_Mobile )
			CheckForAutoAttack();
	}

	private void CheckForAutoAttack()
	{
		if( pawn.Downed )
			return;

		//Don't auto-attack while warming up etc
		if( pawn.stances.FullBodyBusy )
			return;
			
		collideWithPawns = false;

		//Note: While bursting, there seems to be a gap where the pawn becomes mobile?
		bool canDoViolence = pawn.story == null	|| !pawn.story.WorkTagIsDisabled(WorkTags.Violent);

		bool shouldFightFires = pawn.RaceProps.ToolUser
								&& pawn.Faction == Faction.OfPlayer
								&& !pawn.story.WorkTagIsDisabled(WorkTags.Firefighting);

		if( canDoViolence || shouldFightFires )
		{
			//Melee attack adjacent enemy pawns
			//Barring that, put out fires
			Fire targetFire = null;
			for( int i=0; i<9; i++ )
			{
				IntVec3 neigh = pawn.Position + GenAdj.AdjacentCellsAndInside[i];

				if( !neigh.InBounds(pawn.Map) )
					continue;

				var things = neigh.GetThingList(Map);
				for( int j=0; j<things.Count; j++ )
				{
					if( canDoViolence )
					{
						//We just hit the first pawn we see (north first)
						Pawn otherPawn = things[j] as Pawn;
						if( otherPawn != null
							&& !otherPawn.Downed
							&& pawn.HostileTo(otherPawn)  )
						{
							pawn.meleeVerbs.TryMeleeAttack(otherPawn);
							collideWithPawns = true;
							return;
						}
					}

					//Prioritize the fire we're standing on.
					//If there isn't one, prioritize the smallest fire
					//This algorithm assumes that the inside cell is last
					if( shouldFightFires )
					{
						Fire fire = things[j] as Fire;
						if( fire != null
							&& (targetFire == null || fire.fireSize < targetFire.fireSize || i==8 )
							&& (fire.parent == null || fire.parent != pawn) )
							targetFire = fire;
					}
				}
			}

			//We didn't do a melee attack, so see if we found a fire to beat
			if( targetFire != null && (!pawn.InMentalState || pawn.MentalState.def.allowBeatfire) )
			{
				pawn.natives.TryBeatFire( targetFire );
				return;
			}

			//Shoot at the closest enemy in range
			if( canDoViolence
				&& pawn.Faction != null
				&& job.def == JobDefOf.Wait_Combat
				&& (pawn.drafter == null || pawn.drafter.FireAtWill) )
			{
				var attackVerb = pawn.CurrentEffectiveVerb;

				if( attackVerb != null && !attackVerb.verbProps.IsMeleeAttack )
				{
					//We increase the range because we can hit targets slightly outside range by shooting at their ShootableSquares
					//We could just put the range at int.MaxValue but this is slightly more optimized so whatever
					var flags = TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat;
					if( attackVerb.IsIncendiary() )
						flags |= TargetScanFlags.NeedNonBurning;
					
					var curTarg = (Thing)AttackTargetFinder.BestShootTargetFromCurrentPosition(pawn, flags);
                     
					if( curTarg != null )
					{
						pawn.TryStartAttack( curTarg );
						collideWithPawns = true;
						return;
					}
				}
			}
		}
	}
}}



