using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace RimWorld{

public enum RotStage : byte
{
	Fresh,
	Rotting,
	Dessicated,
}

public static class RottableUtility
{
	public static bool IsNotFresh( this Thing t )
	{
		var cr = t.TryGetComp<CompRottable>();
		return cr != null && cr.Stage != RotStage.Fresh;
	}

	public static bool IsDessicated( this Thing t )
	{
		var cr = t.TryGetComp<CompRottable>();
		return cr != null && cr.Stage == RotStage.Dessicated;
	}

	public static RotStage GetRotStage( this Thing t )
	{
		var cr = t.TryGetComp<CompRottable>();

		if( cr == null )
			return RotStage.Fresh;
		
		return cr.Stage;
	}
}


public class CompRottable : ThingComp
{
	//Working vars
	private float rotProgressInt = 0f;

	//Properties
	public CompProperties_Rottable PropsRot { get { return (CompProperties_Rottable)props; } }
	public float RotProgressPct { get { return RotProgress / PropsRot.TicksToRotStart; } }
	public float RotProgress
	{
		get { return rotProgressInt; }
		set
		{
			var prevStage = Stage;

			rotProgressInt = value;

			if( prevStage != Stage )
				StageChanged();
		}
	}
	public RotStage Stage
	{
		get
		{
			if( RotProgress < PropsRot.TicksToRotStart )
				return RotStage.Fresh;
			else if( RotProgress < PropsRot.TicksToDessicated )
				return RotStage.Rotting;
			else
				return RotStage.Dessicated;
		}
	}
	public int TicksUntilRotAtCurrentTemp
	{
		get 
		{
			float cellTemp = parent.AmbientTemperature;
			cellTemp = Mathf.RoundToInt(cellTemp); //Rounding here reduces dithering

            return TicksUntilRotAtTemp(cellTemp);
		}
	}
	public bool Active
	{
		get
		{
			if( PropsRot.disableIfHatcher )
			{
				var hatcher = parent.TryGetComp<CompHatcher>();
				if( hatcher != null && !hatcher.TemperatureDamaged )
					return false;
			}

			return true;
		}
	}

	public override void PostExposeData()
	{
		base.PostExposeData();

		Scribe_Values.Look(ref rotProgressInt, "rotProg");
	}

	public override void CompTick()
	{
		Tick(1);
	}

	public override void CompTickRare()
	{
		Tick(GenTicks.TickRareInterval);
	}

	private void Tick(int interval)
	{
		if( !Active )
			return;

        float previousProgress = RotProgress;

        // Do rotting progress according to temperature
        float cellTemp = parent.AmbientTemperature;
        float rotRate = GenTemperature.RotRateAtTemperature(cellTemp);
		RotProgress += rotRate * interval;

		//Destroy if needed
		//Should this be in StageChanged?
		if( Stage == RotStage.Rotting && PropsRot.rotDestroys )
		{
			if( parent.IsInAnyStorage() && parent.SpawnedOrAnyParentSpawned )
			{
				Messages.Message( "MessageRottedAwayInStorage".Translate(parent.Label, parent).CapitalizeFirst(), new TargetInfo(parent.PositionHeld, parent.MapHeld), MessageTypeDefOf.NegativeEvent);
				LessonAutoActivator.TeachOpportunity( ConceptDefOf.SpoilageAndFreezers, OpportunityType.GoodToKnow );
			}

			parent.Destroy();
			return;
		}

		//Once per day...
        bool isNewDay = Mathf.FloorToInt(previousProgress / GenDate.TicksPerDay) != Mathf.FloorToInt(RotProgress / GenDate.TicksPerDay);
		if( isNewDay && ShouldTakeRotDamage() )
		{
			if( Stage == RotStage.Rotting && PropsRot.rotDamagePerDay > 0 )
				parent.TakeDamage(new DamageInfo(DamageDefOf.Rotting, GenMath.RoundRandom(PropsRot.rotDamagePerDay)));
			else if( Stage == RotStage.Dessicated && PropsRot.dessicatedDamagePerDay > 0 )
				parent.TakeDamage(new DamageInfo(DamageDefOf.Rotting, GenMath.RoundRandom(PropsRot.dessicatedDamagePerDay)));
		}
	}

	private bool ShouldTakeRotDamage()
	{
		//We don't take dessicated damage if contained in a deterioration-preventing building like a grave or sarcophagus
		//preventDeterioration covers dessicated damage because dessicated damage is basically a simulation of deterioration
		var t = parent.ParentHolder as Thing;
		if( t != null && t.def.category == ThingCategory.Building && t.def.building.preventDeteriorationInside )
			return false;

		return true;
	}

	public override void PreAbsorbStack(Thing otherStack, int count)
	{
		//New rot progress is the weighted average of our old rot progresses
		float proportionOther = (float)count/ (float)(parent.stackCount + count);

		float otherRotProg = ((ThingWithComps)otherStack).GetComp<CompRottable>().RotProgress;

		RotProgress = Mathf.Lerp(RotProgress, otherRotProg, proportionOther);
	}

	public override void PostSplitOff(Thing piece)
	{
		//Piece inherits my rot progress
		((ThingWithComps)piece).GetComp<CompRottable>().RotProgress = RotProgress;
	}

	public override void PostIngested( Pawn ingester )
	{
		if( Stage != RotStage.Fresh )
			FoodUtility.AddFoodPoisoningHediff(ingester, parent, FoodPoisonCause.Rotten);
	}

	public override string CompInspectStringExtra()
	{
		if( !Active )
			return null;

		var sb = new StringBuilder();

		switch( Stage)
		{
			case RotStage.Fresh:		sb.Append("RotStateFresh".Translate() + "."); break;
			case RotStage.Rotting:		sb.Append("RotStateRotting".Translate() + "."); break;
			case RotStage.Dessicated:	sb.Append("RotStateDessicated".Translate() + "."); break;
		}

		float progressUntilStartRot = PropsRot.TicksToRotStart - RotProgress;
        if(progressUntilStartRot > 0)
        {
            float cellTemp = parent.AmbientTemperature;
			cellTemp = Mathf.RoundToInt(cellTemp);//Rounding here reduces dithering
            float rotRate = GenTemperature.RotRateAtTemperature(cellTemp);

			int ticksUntilStartRot = TicksUntilRotAtCurrentTemp;

			sb.AppendLine();
            if( rotRate < 0.001f )
            {
                // frozen
                sb.Append( "CurrentlyFrozen".Translate() + "." );
            }
            else if( rotRate < 0.999f )
            {
				// refrigerated
				sb.Append( "CurrentlyRefrigerated".Translate(ticksUntilStartRot.ToStringTicksToPeriod()) + "." );
            }
            else
            {
                // not refrigerated
				sb.Append("NotRefrigerated".Translate(ticksUntilStartRot.ToStringTicksToPeriod()) + ".");
            }
        }
		
		return sb.ToString();
	}

    public int ApproxTicksUntilRotWhenAtTempOfTile(int tile, int ticksAbs)
    {
		//Note that we ignore local map temperature offsets even if there's a map at this tile
        float temp = GenTemperature.GetTemperatureFromSeasonAtTile(ticksAbs, tile);

        return TicksUntilRotAtTemp(temp);
    }

    public int TicksUntilRotAtTemp(float temp)
    {
		if( !Active )
			return GenDate.TicksPerYear * 20;

        float rotRate = GenTemperature.RotRateAtTemperature(temp);

        if( rotRate <= 0 )
            return GenDate.TicksPerYear * 20; //Will never rot. Just return a huge value. Hacky

        float progressUntilStartRot = PropsRot.TicksToRotStart - RotProgress;
        if( progressUntilStartRot <= 0 )
            return 0; //Already rotten

        return Mathf.RoundToInt(progressUntilStartRot / rotRate);
    }

	private void StageChanged()
	{
		var corpse = parent as Corpse;

		if( corpse != null )
			corpse.RotStageChanged();
	}

	public void RotImmediately()
	{
		if( RotProgress < PropsRot.TicksToRotStart )
			RotProgress = PropsRot.TicksToRotStart;
	}
}
}

