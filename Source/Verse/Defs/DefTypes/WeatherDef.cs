using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse.Sound;
using UnityEngine;
using RimWorld;

namespace Verse{

public enum Favorability : byte
{
	VeryBad,
	Bad,
	Neutral,
	Good,
	VeryGood,
}

public class WeatherDef : Def
{
	//When this weather should appear
	public IntRange					durationRange = new IntRange(16000, 160000);
	public bool						repeatable = false;
	public Favorability				favorability = Favorability.Neutral;
	public FloatRange               temperatureRange = new FloatRange(-999f, 999f);
	public SimpleCurve				commonalityRainfallFactor = null;

	//Gameplay effects
	public float					rainRate = 0; //1 = raining
	public float					snowRate = 0; //1 = snowing
	public float					windSpeedFactor = 1;	//1 = calm weather
	public float					moveSpeedMultiplier = 1f;
	public float					accuracyMultiplier = 1f;
	public float					perceivePriority; //determines which weather is reported during transitions
	public ThoughtDef				exposedThought = null;

	//Audiovisuals
	public List<SoundDef>			ambientSounds = new List<SoundDef>();
	public List<WeatherEventMaker>	eventMakers = new List<WeatherEventMaker>();
	public List<Type>				overlayClasses = new List<Type>();
    
	//Weather core
	public SkyColorSet				skyColorsNightMid;
	public SkyColorSet				skyColorsNightEdge;
	public SkyColorSet				skyColorsDay;
	public SkyColorSet				skyColorsDusk;

	//Calculated
	[Unsaved]	private WeatherWorker			workerInt;

	//Properties
	public WeatherWorker Worker
	{
		get
		{
			if( workerInt == null )
				workerInt = new WeatherWorker(this);
			return workerInt;
		}
	}

	public override void PostLoad()
	{
		base.PostLoad();

		workerInt = new WeatherWorker(this);
	}

	public override IEnumerable<string> ConfigErrors()
	{
		if( skyColorsDay.saturation == 0 || skyColorsDusk.saturation == 0 || skyColorsNightMid.saturation == 0 || skyColorsNightEdge.saturation == 0 )
			yield return "a sky color has saturation of 0";
	}

	public static WeatherDef Named( string defName )
	{
		return DefDatabase<WeatherDef>.GetNamed( defName );
	}
}
}


