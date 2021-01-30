using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using Verse.AI;
using RimWorld;


namespace Verse{
public enum CheckJobOverrideOnDamageMode
{
	Never,
	OnlyIfInstigatorNotJobTarget,
	Always
}

public class JobDef : Def
{
	//Globals
	public Type				driverClass;
	[MustTranslate] public string reportString = "Doing something.";
	public bool				playerInterruptible = true;
	public CheckJobOverrideOnDamageMode checkOverrideOnDamage = CheckJobOverrideOnDamageMode.Always;
	public bool				alwaysShowWeapon = false;
	public bool				neverShowWeapon = false;
	public bool				suspendable = true;						//Set to false when job code is complex and cannot be suspended and restarted
	public bool				casualInterruptible = true;
	public bool				allowOpportunisticPrefix = false;
	public bool				collideWithPawns = false;
	public bool				isIdle = false;
	public TaleDef			taleOnCompletion = null;
	public bool				neverFleeFromEnemies;

	//Misc
	public bool				makeTargetPrisoner = false;

	//Joy
	public int				joyDuration = 4000;
	public int				joyMaxParticipants = 1;
	public float			joyGainRate = 1;
	public SkillDef			joySkill = null;
	public float			joyXpPerTick = 0;
	public JoyKindDef		joyKind = null;
	public Rot4				faceDir = Rot4.Invalid;
	
	public override IEnumerable<string> ConfigErrors()
	{
		foreach( var e in base.ConfigErrors() )
		{
			yield return e;
		}

		if( joySkill != null && joyXpPerTick == 0 )
			yield return "funSkill is not null but funXpPerTick is zero";
	}
}}
