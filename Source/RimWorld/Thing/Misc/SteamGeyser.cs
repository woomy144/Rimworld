using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorld{

public class Building_SteamGeyser : Building
{
	//Components
	private IntermittentSteamSprayer steamSprayer;

	//Working vars
	public Building harvester = null;	//set externally
	private Sustainer spraySustainer = null;
	private int spraySustainerStartTick = -999;




	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);

		steamSprayer = new IntermittentSteamSprayer(this);
		steamSprayer.startSprayCallback = StartSpray;
		steamSprayer.endSprayCallback = EndSpray;

	}

	private void StartSpray()
	{
		SnowUtility.AddSnowRadial(this.OccupiedRect().RandomCell, Map, 4, -0.06f);

		spraySustainer = SoundStarter.TrySpawnSustainer( SoundDefOf.GeyserSpray, new TargetInfo(Position, Map) );
		spraySustainerStartTick = Find.TickManager.TicksGame;
	}

	private void EndSpray()
	{
		if( spraySustainer != null )
		{
			spraySustainer.End();
			spraySustainer = null;
		}
	}

	public override void Tick()
	{
		if( harvester == null )
			steamSprayer.SteamSprayerTick();

		//Saftey catch
		if( spraySustainer != null )
		{
			if( Find.TickManager.TicksGame > spraySustainerStartTick + 1000)
			{
				Log.Message("Geyser spray sustainer still playing after 1000 ticks. Force-ending.");
				spraySustainer.End();
				spraySustainer = null;
			}
		}
	}
}



public class IntermittentSteamSprayer
{
	//Links
	private Thing parent;

	//Working vars
	int ticksUntilSpray = MinTicksBetweenSprays;
	int sprayTicksLeft = 0;
	public Action startSprayCallback = null;
	public Action endSprayCallback = null;

	//Constants
	private const int MinTicksBetweenSprays = 500;
	private const int MaxTicksBetweenSprays = 2000;
	private const int MinSprayDuration = 200;
	private const int MaxSprayDuration = 500;
	private const float SprayThickness = 0.6f;

	public IntermittentSteamSprayer(Thing parent)
	{
		this.parent = parent;
	}

	public void SteamSprayerTick()
	{
		if( sprayTicksLeft > 0 )
		{
			sprayTicksLeft--;

			//Do spray effect
			if( Rand.Value < SprayThickness )					
				MoteMaker.ThrowAirPuffUp( parent.TrueCenter(), parent.Map );	

			//Push some heat
			if( Find.TickManager.TicksGame % 20 == 0 )
			{
				GenTemperature.PushHeat(parent, 40 );
			}

			//Done spraying
			if( sprayTicksLeft <= 0 )
			{
				if( endSprayCallback != null )
					endSprayCallback();

				ticksUntilSpray = Rand.RangeInclusive( MinTicksBetweenSprays, MaxTicksBetweenSprays );
			}
		}
		else
		{
			ticksUntilSpray--;

			if( ticksUntilSpray <= 0 )
			{
				//Start spray
				if( startSprayCallback != null )
					startSprayCallback();
				
				sprayTicksLeft = Rand.RangeInclusive( MinSprayDuration, MaxSprayDuration );
			}
		}
	}
}}

