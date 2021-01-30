using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Verse;
using RimWorld;



namespace Verse.AI{
public class JobDriver_AttackStatic : JobDriver
{	
	//Working vars
	private bool startedIncapacitated;
	private int numAttacksMade;

	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref startedIncapacitated, "startedIncapacitated");
		Scribe_Values.Look(ref numAttacksMade, "numAttacksMade");
	}

	public override bool TryMakePreToilReservations(bool errorOnFailed)
	{
		return true;
	}

	protected override IEnumerable<Toil> MakeNewToils()
	{
		yield return Toils_Misc.ThrowColonistAttackingMote(TargetIndex.A);

		Toil init = new Toil();
		init.initAction = ()=>
		{
			Pawn targetPawn = TargetThingA as Pawn;
			if( targetPawn != null )
				startedIncapacitated = targetPawn.Downed;

			pawn.pather.StopDead();
		};
		init.tickAction = ()=>
		{
			//End job in success if:
			//  target null
			//  target destroyed
			//  pawn target is incapped (where he wasn't when the job was initiated)
			if( !TargetA.IsValid )
			{
				EndJobWith(JobCondition.Succeeded);
				return;
			}
			if (TargetA.HasThing)
			{
				Pawn targetPawn = TargetA.Thing as Pawn;
				if (TargetA.Thing.Destroyed
					|| (targetPawn != null && !startedIncapacitated && targetPawn.Downed))
				{
					EndJobWith(JobCondition.Succeeded);
					return;
				}
			}

			if( numAttacksMade >= job.maxNumStaticAttacks && !pawn.stances.FullBodyBusy )
			{
				EndJobWith(JobCondition.Succeeded);
				return;
			}

			if( pawn.TryStartAttack(TargetA) )
			{
				numAttacksMade++;
				//Note that we don't end the job yet even if numAttacksGame >= maxNumStaticAttacks,
				//because we wait until the attack is finished
			}
			else if( !pawn.stances.FullBodyBusy )
			{
				var verb = pawn.TryGetAttackVerb(TargetA.Thing, !pawn.IsColonist);

				if( job.endIfCantShootTargetFromCurPos
					&& (verb == null || !verb.CanHitTargetFrom(pawn.Position, TargetA)) )
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}
				
				if( job.endIfCantShootInMelee )
				{
					if( verb == null )
					{
						EndJobWith(JobCondition.Incompletable);
						return;
					}
					else
					{
						float effectiveMinRange = verb.verbProps.EffectiveMinRange(TargetA, pawn);
						
						if( pawn.Position.DistanceToSquared(TargetA.Cell) < effectiveMinRange * effectiveMinRange
							&& pawn.Position.AdjacentTo8WayOrInside(TargetA.Cell) )
						{
							EndJobWith(JobCondition.Incompletable);
							return;
						}
					}
				}
			}
		};
		init.defaultCompleteMode = ToilCompleteMode.Never;
		yield return init;
	}
}}