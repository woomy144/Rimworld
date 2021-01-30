using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;




namespace Verse.AI{
public static class ToilFailConditions
{
	public static Toil FailOn( this Toil toil, Func<Toil, bool> condition )
	{
		toil.AddEndCondition( () => 
			{
				if( condition(toil) )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return toil;
	}

	public static T FailOn<T>( this T f, Func<bool> condition ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
		{
			if(condition())
				return JobCondition.Incompletable;
			return JobCondition.Ongoing;
		});
		return f;
	}

	public static T FailOnDestroyedOrNull<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=> 
			{
				if( f.GetActor().jobs.curJob.GetTarget( ind).Thing.DestroyedOrNull() )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnDespawnedOrNull<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var target = f.GetActor().jobs.curJob.GetTarget(ind);
				var t = target.Thing;

				// if there is no thing but the target is valid (it's a cell) then don't end the job
				if( t == null && target.IsValid )
					return JobCondition.Ongoing;

				// note: if the target is spawned in another map, then from the actor's perspective it's unspawned, so we end the job
				if( t == null || !t.Spawned || t.Map != f.GetActor().Map )
					return JobCondition.Incompletable;

				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T EndOnDespawnedOrNull<T>( this T f, TargetIndex ind, JobCondition endCondition = JobCondition.Incompletable ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var target = f.GetActor().jobs.curJob.GetTarget(ind);
				var t = target.Thing;

				// if there is no thing but the target is valid (it's a cell) then don't end the job
				if( t == null && target.IsValid )
					return JobCondition.Ongoing;

				// note: if the target is spawned in another map, then from the actor's perspective it's unspawned, so we end the job
				if( t == null || !t.Spawned || t.Map != f.GetActor().Map )
					return endCondition;

				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T EndOnNoTargetInQueue<T>(this T f, TargetIndex ind, JobCondition endCondition = JobCondition.Incompletable) where T : IJobEndable
	{
		f.AddEndCondition(() =>
			{
				var actor = f.GetActor();
				var curJob = actor.jobs.curJob;
				var queue = curJob.GetTargetQueue(ind);

				if( queue.NullOrEmpty() )
					return endCondition;
				else
					return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnDowned<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var t = f.GetActor().jobs.curJob.GetTarget(ind).Thing;
				if( ((Pawn)t).Downed )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnMobile<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var t = f.GetActor().jobs.curJob.GetTarget(ind).Thing;
				if( ((Pawn)t).health.State == PawnHealthState.Mobile )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnNotDowned<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var t = f.GetActor().jobs.curJob.GetTarget(ind).Thing;
				if( !((Pawn)t).Downed )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnNotAwake<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var t = f.GetActor().jobs.curJob.GetTarget(ind).Thing;
				if( !((Pawn)t).Awake() )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnNotCasualInterruptible<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var t = f.GetActor().jobs.curJob.GetTarget(ind).Thing;
				if( !((Pawn)t).CanCasuallyInteractNow() )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnMentalState<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var p = f.GetActor().jobs.curJob.GetTarget(ind).Thing as Pawn;
				if( p != null && p.InMentalState )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnAggroMentalState<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var p = f.GetActor().jobs.curJob.GetTarget(ind).Thing as Pawn;
				if( p != null && p.InAggroMentalState )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}
	
	public static T FailOnAggroMentalStateAndHostile<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var p = f.GetActor().jobs.curJob.GetTarget(ind).Thing as Pawn;
				if( p != null && p.InAggroMentalState && p.HostileTo(f.GetActor()) )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnSomeonePhysicallyInteracting<T>(this T f, TargetIndex ind) where T : IJobEndable
	{
		f.AddEndCondition( () =>
			{
				var actor = f.GetActor();
				var t = actor.jobs.curJob.GetTarget(ind).Thing;

				if( t != null
					&& actor.Map.physicalInteractionReservationManager.IsReserved(t)
					&& !actor.Map.physicalInteractionReservationManager.IsReservedBy(actor, t) )
				{
					return JobCondition.Incompletable;
				}

				return JobCondition.Ongoing;
			});

		return f;
	}

	public static T FailOnForbidden<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
		{
			Pawn actor = f.GetActor();
			if( actor.Faction != Faction.OfPlayer )
				return JobCondition.Ongoing;
			
			if( actor.jobs.curJob.ignoreForbidden )
				return JobCondition.Ongoing;

			var thing = actor.jobs.curJob.GetTarget(ind).Thing;

			if( thing == null )
				return JobCondition.Ongoing;

			if( thing.IsForbidden( actor ) )
				return JobCondition.Incompletable;

			return JobCondition.Ongoing;
		});
		return f;
	}

	public static T FailOnDespawnedNullOrForbidden<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.FailOnDespawnedOrNull(ind);
		f.FailOnForbidden(ind);
		return f;
	}

	public static T FailOnDestroyedNullOrForbidden<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.FailOnDestroyedOrNull(ind);
		f.FailOnForbidden(ind);
		return f;
	}

	public static T FailOnThingMissingDesignation<T>( this T f, TargetIndex ind, DesignationDef desDef ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var actor = f.GetActor();
				var job = actor.jobs.curJob;

				if( job.ignoreDesignations )
 					return JobCondition.Ongoing;

				var targ = job.GetTarget(ind).Thing;

				if( targ == null || actor.Map.designationManager.DesignationOn(targ, desDef ) == null )
					return JobCondition.Incompletable;

				return JobCondition.Ongoing;
			}
		);
		return f;
	}

	public static T FailOnThingHavingDesignation<T>( this T f, TargetIndex ind, DesignationDef desDef ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var actor = f.GetActor();
				var job = actor.jobs.curJob;

				if( job.ignoreDesignations )
 					return JobCondition.Ongoing;

				var targ = job.GetTarget(ind).Thing;

				if( targ == null || actor.Map.designationManager.DesignationOn(targ, desDef ) != null )
					return JobCondition.Incompletable;

				return JobCondition.Ongoing;
			}
		);
		return f;
	}

	public static T FailOnCellMissingDesignation<T>( this T f, TargetIndex ind, DesignationDef desDef ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				var actor = f.GetActor();
				var job =  actor.jobs.curJob;
				if( job.ignoreDesignations )
 					return JobCondition.Ongoing;
				if( actor.Map.designationManager.DesignationAt(job.GetTarget(ind).Cell, desDef ) == null )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			}
		);
		return f;
	}

	public static T FailOnBurningImmobile<T>( this T f, TargetIndex ind ) where T : IJobEndable
	{
		f.AddEndCondition( ()=> 
			{
				if(f.GetActor().jobs.curJob.GetTarget(ind).ToTargetInfo(f.GetActor().Map).IsBurning() )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	public static T FailOnCannotTouch<T>(this T f, TargetIndex ind, PathEndMode peMode) where T : IJobEndable
	{
		f.AddEndCondition(() =>
			{
				if( !f.GetActor().CanReachImmediate(f.GetActor().jobs.curJob.GetTarget(ind), peMode) )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}
	
	public static T FailOnIncapable<T>( this T f, PawnCapacityDef pawnCapacity ) where T : IJobEndable
	{
		f.AddEndCondition( ()=>
			{
				if( !f.GetActor().health.capacities.CapableOf(pawnCapacity) )
					return JobCondition.Incompletable;
				return JobCondition.Ongoing;
			});
		return f;
	}

	//======================================================================================
	//================================ Toil-only conditions ================================
	//======================================================================================

	public static Toil FailOnDespawnedNullOrForbiddenPlacedThings( this Toil toil )
	{
		toil.AddFailCondition( ()=>
            {
                if(toil.actor.jobs.curJob.placedThings == null)
                    return false;
                
				for( int i = 0; i < toil.actor.jobs.curJob.placedThings.Count; i++ )
        		{
					var targ = toil.actor.jobs.curJob.placedThings[i];

					if( targ.thing == null
						|| !targ.thing.Spawned
						|| targ.thing.Map != toil.actor.Map // note: if the target is spawned in another map, then from the actor's perspective it's unspawned, so we end the job
						|| (!toil.actor.CurJob.ignoreForbidden && targ.thing.IsForbidden(toil.actor)) )
					{
						return true;
					}
        		}
                
        		return false;
		    }
        );
		return toil;
	}
}}

