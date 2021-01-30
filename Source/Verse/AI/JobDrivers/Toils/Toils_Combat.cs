using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;

namespace Verse.AI{
public static class Toils_Combat
{
	public static Toil TrySetJobToUseAttackVerb(TargetIndex targetInd)
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				Pawn actor = toil.actor;
				Job curJob = actor.jobs.curJob;
                bool allowManualCastWeapons = !actor.IsColonist;

				Verb verb = actor.TryGetAttackVerb(curJob.GetTarget(targetInd).Thing, allowManualCastWeapons);

				if( verb == null )
				{
					actor.jobs.EndCurrentJob( JobCondition.Incompletable);
					return;
				}

				curJob.verbToUse = verb;
			};
		return toil;
	}
	

	public static Toil GotoCastPosition( TargetIndex targetInd, bool closeIfDowned = false, float maxRangeFactor = 1f )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				Pawn actor = toil.actor;
				Job curJob = actor.jobs.curJob;
				Thing targThing = curJob.GetTarget(targetInd).Thing;
				Pawn targPawn = targThing as Pawn;

				//We get closer if the target is downed and we can
				CastPositionRequest req = new CastPositionRequest();
				req.caster = toil.actor;
				req.target = targThing;
				req.verb = curJob.verbToUse;
				req.maxRangeFromTarget = (!closeIfDowned||targPawn==null||!targPawn.Downed)
					? Mathf.Max( curJob.verbToUse.verbProps.range * maxRangeFactor, ShootTuning.MeleeRange )
					: Mathf.Min( curJob.verbToUse.verbProps.range, targPawn.RaceProps.executionRange );
				req.wantCoverFromTarget = false;
				
				IntVec3 dest;
				if( !CastPositionFinder.TryFindCastPosition( req, out dest ) )
				{
					toil.actor.jobs.EndCurrentJob( JobCondition.Incompletable );
					return;
				}

				toil.actor.pather.StartPath( dest, PathEndMode.OnCell );

				actor.Map.pawnDestinationReservationManager.Reserve( actor, curJob, dest );
			};
		toil.FailOnDespawnedOrNull( targetInd );
		toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;

		return toil;
	}

	public static Toil CastVerb(TargetIndex targetInd, bool canHitNonTargetPawns = true)
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				toil.actor.jobs.curJob.verbToUse.TryStartCastOn(toil.actor.jobs.curJob.GetTarget(targetInd), canHitNonTargetPawns: canHitNonTargetPawns);
			};
		toil.defaultCompleteMode = ToilCompleteMode.FinishedBusy;
		return toil;
	}

	public static Toil FollowAndMeleeAttack(TargetIndex targetInd, Action hitAction)
	{
		//Follow and attack victim
		Toil followAndAttack = new Toil();
		followAndAttack.tickAction = ()=>
			{
				Pawn actor = followAndAttack.actor;
				Job curJob = actor.jobs.curJob;
				JobDriver driver = actor.jobs.curDriver;
				Thing victim = curJob.GetTarget(targetInd).Thing;
				Pawn victimPawn = victim as Pawn;

				if( !victim.Spawned )
				{
					driver.ReadyForNextToil();
					return;
				}

				if( victim != actor.pather.Destination.Thing
					|| (!actor.pather.Moving && !actor.CanReachImmediate(victim, PathEndMode.Touch)) )
				{
					actor.pather.StartPath( victim, PathEndMode.Touch );
				}
				else
				{
					if( actor.CanReachImmediate(victim, PathEndMode.Touch) )
					{
						//Do not attack downed people unless the job specifies to do so
						if( victimPawn != null && victimPawn.Downed && !curJob.killIncappedTarget )
						{
							driver.ReadyForNextToil();
							return;
						}

						//Try to hit them
						hitAction();
					}
				}
			};
		followAndAttack.defaultCompleteMode = ToilCompleteMode.Never;
		return followAndAttack;
	}
}}



