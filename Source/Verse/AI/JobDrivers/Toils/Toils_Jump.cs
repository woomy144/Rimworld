using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using UnityEngine;


namespace Verse.AI{
public static class Toils_Jump
{
	public static Toil Jump( Toil jumpTarget )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			toil.actor.jobs.curDriver.JumpToToil(jumpTarget);
		};
		return toil;
	}

	public static Toil JumpIf( Toil jumpTarget, Func<bool> condition )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			if( condition() )
				toil.actor.jobs.curDriver.JumpToToil(jumpTarget);
		};
		return toil;
	}

	public static Toil JumpIfTargetDespawnedOrNull( TargetIndex ind, Toil jumpToil )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				var target = toil.actor.jobs.curJob.GetTarget(ind).Thing;
				if( target == null || !target.Spawned )
					toil.actor.jobs.curDriver.JumpToToil(jumpToil);
			};
		return toil;
	}
	
	public static Toil JumpIfTargetInvalid( TargetIndex ind, Toil jumpToil )
	{
		var toil = new Toil();
		toil.initAction = () =>
			{
				var target = toil.actor.jobs.curJob.GetTarget(ind);
				if( !target.IsValid )
					toil.actor.jobs.curDriver.JumpToToil(jumpToil);
			};
		return toil;
	}


	public static Toil JumpIfTargetNotHittable( TargetIndex ind, Toil jumpToil )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				var actor = toil.actor;
				var curJob = actor.jobs.curJob;
				var target = curJob.GetTarget(ind);

				if( curJob.verbToUse == null
					|| !curJob.verbToUse.IsStillUsableBy(actor)
					|| !curJob.verbToUse.CanHitTarget(target) )
				{
					actor.jobs.curDriver.JumpToToil(jumpToil);
				}
			};
		return toil;
	}

	public static Toil JumpIfTargetDowned( TargetIndex ind, Toil jumpToil )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
			{
				var actor = toil.actor;
				var curJob = actor.jobs.curJob;
				var targetPawn = curJob.GetTarget(ind).Thing as Pawn;

				if( targetPawn != null && targetPawn.Downed )
					actor.jobs.curDriver.JumpToToil(jumpToil);
			};
		return toil;
	}

	public static Toil JumpIfHaveTargetInQueue( TargetIndex ind, Toil jumpToil )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			Pawn actor = toil.actor;
			Job curJob = actor.jobs.curJob;
			var queue = curJob.GetTargetQueue(ind);
			if( !queue.NullOrEmpty() )
				actor.jobs.curDriver.JumpToToil(jumpToil);
		};

		return toil;
	}

	public static Toil JumpIfCannotTouch(TargetIndex ind, PathEndMode peMode, Toil jumpToil)
	{
		var toil = new Toil();
		toil.initAction = () =>
			{
				var actor = toil.actor;
				var curJob = actor.jobs.curJob;
				var target = curJob.GetTarget(ind);

				if( !actor.CanReachImmediate(target, peMode) )
					actor.jobs.curDriver.JumpToToil(jumpToil);
			};
		return toil;
	}
}}