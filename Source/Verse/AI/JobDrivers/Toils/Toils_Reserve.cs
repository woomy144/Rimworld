using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace Verse.AI
{

public static class Toils_Reserve
{
	public static Toil Reserve( TargetIndex ind, int maxPawns = 1, int stackCount = ReservationManager.StackCount_All, ReservationLayerDef layer = null )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			if( !toil.actor.Reserve(toil.actor.jobs.curJob.GetTarget(ind), toil.actor.CurJob, maxPawns, stackCount, layer ) )
				toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
		};
		toil.defaultCompleteMode = ToilCompleteMode.Instant;
		toil.atomicWithPrevious = true;
		return toil;
	}

	public static Toil ReserveQueue( TargetIndex ind, int maxPawns = 1, int stackCount = ReservationManager.StackCount_All, ReservationLayerDef layer = null )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			var queue = toil.actor.jobs.curJob.GetTargetQueue(ind);
			if( queue != null )
			{
				for( int i=0; i<queue.Count; i++ )
				{
					if( !toil.actor.Reserve(queue[i], toil.actor.CurJob, maxPawns, stackCount, layer ) )
						toil.actor.jobs.EndCurrentJob(JobCondition.Incompletable);
				}
			}
		};
		toil.defaultCompleteMode = ToilCompleteMode.Instant;
		toil.atomicWithPrevious = true;
		return toil;
	}
	
	public static Toil Release( TargetIndex ind )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			toil.actor.Map.reservationManager.Release( toil.actor.jobs.curJob.GetTarget(ind), toil.actor, toil.actor.CurJob );
		};
		toil.defaultCompleteMode = ToilCompleteMode.Instant;
		toil.atomicWithPrevious = true;
		return toil;
	}


}}

