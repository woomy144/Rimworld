using UnityEngine;
using System.Collections;
using Verse;
using Verse.Sound;


namespace RimWorld{
public class CompExplosive : ThingComp
{
	//Working vars
	public bool 			wickStarted = false;
	protected int			wickTicksLeft = 0;
	private Thing			instigator;
	public bool				destroyedThroughDetonation;
	
	//Components
	protected Sustainer		wickSoundSustainer = null;
	
	//Properties
	public CompProperties_Explosive Props { get { return (CompProperties_Explosive)props; } }
	protected int StartWickThreshold
	{
		get
		{
			return Mathf.RoundToInt(Props.startWickHitPointsPercent * parent.MaxHitPoints);
		}
	}
	private bool CanEverExplodeFromDamage
	{
		get
		{
			if( Props.chanceNeverExplodeFromDamage < 0.00001f )
				return true;
			else
			{
				Rand.PushState();
				Rand.Seed = parent.thingIDNumber.GetHashCode();
				bool result = Rand.Value < Props.chanceNeverExplodeFromDamage;
				Rand.PopState();
				return result;
			}
		}
	}


	public override void PostExposeData()
	{
		base.PostExposeData();

		Scribe_References.Look(ref instigator, "instigator");
		Scribe_Values.Look( ref wickStarted, "wickStarted", false );
		Scribe_Values.Look( ref wickTicksLeft, "wickTicksLeft", 0 );
		Scribe_Values.Look( ref destroyedThroughDetonation, "destroyedThroughDetonation" );
	}

	public override void CompTick()
	{
		if( wickStarted )
		{
			if( wickSoundSustainer == null )
				StartWickSustainer(); //or sustainer is missing on load
			else
				wickSoundSustainer.Maintain();
			
			wickTicksLeft--;
			if( wickTicksLeft <= 0 )
				Detonate(parent.MapHeld);
		}
	}
	
	private void StartWickSustainer()
	{
		SoundDefOf.MetalHitImportant.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
		SoundInfo info = SoundInfo.InMap(parent, MaintenanceType.PerTick);
		wickSoundSustainer = SoundDefOf.HissSmall.TrySpawnSustainer( info );
	}

	private void EndWickSustainer()
	{
		if( wickSoundSustainer != null )
		{
			wickSoundSustainer.End();
			wickSoundSustainer = null;
		}
	}

	public override void PostDraw()
	{
		if( wickStarted )
			parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.BurningWick);
	}

	public override void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
	{
		absorbed = false;

		if( CanEverExplodeFromDamage )
		{
			if( dinfo.Def.ExternalViolenceFor(parent) && dinfo.Amount >= parent.HitPoints && CanExplodeFromDamageType(dinfo.Def) )
			{
				//Explode immediately from excessive incoming damage
				//Must happen here, before I'm destroyed. I can't do it after because I lose my map reference.
				if( parent.MapHeld != null )
				{
					Detonate(parent.MapHeld);
					
					// if we haven't actually died, just let the standard damage code take care of it
					if( parent.Destroyed )
						absorbed = true;
				}
			}
			else if( !wickStarted && Props.startWickOnDamageTaken != null && dinfo.Def == Props.startWickOnDamageTaken )
			{
				//Start wick for special damage type?
				StartWick(dinfo.Instigator);
			}
		}
	}
	
	public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
	{
		if( !CanEverExplodeFromDamage )
			return;
		
		if( !CanExplodeFromDamageType(dinfo.Def) )
			return;
		
		if( !parent.Destroyed )
		{
			if( wickStarted && dinfo.Def == DamageDefOf.Stun )	//Stop wick on stun damage
				StopWick();
			else if( !wickStarted && parent.HitPoints <= StartWickThreshold ) //Start wick on damage below threshold
			{
				if( dinfo.Def.ExternalViolenceFor(parent) )
					StartWick(dinfo.Instigator);
			}
		}
	}
	
	public void StartWick(Thing instigator = null)
	{
		if( wickStarted )
			return;
		
		if( ExplosiveRadius() <= 0 )
			return;

		this.instigator = instigator;

		wickStarted = true;
		wickTicksLeft = Props.wickTicks.RandomInRange;
		StartWickSustainer();

		GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(parent, Props.explosiveDamageType);
	}
	
	public void StopWick()
	{
		wickStarted = false;
		instigator = null;
	}

	public float ExplosiveRadius()
	{
		var props = Props;

		//Expand radius for stackcount
		float radius = props.explosiveRadius;
		if( parent.stackCount > 1 && props.explosiveExpandPerStackcount > 0 )
			radius += Mathf.Sqrt((parent.stackCount-1) * props.explosiveExpandPerStackcount);
		if( props.explosiveExpandPerFuel > 0 && parent.GetComp<CompRefuelable>() != null )
			radius += Mathf.Sqrt(parent.GetComp<CompRefuelable>().Fuel * props.explosiveExpandPerFuel);

		return radius;
	}
	
	protected void Detonate(Map map)
	{	
		if( !parent.SpawnedOrAnyParentSpawned )
			return;

		var props = Props;
		float radius = ExplosiveRadius();

		// Do this before destroying it so the fuel doesn't end up on the ground
		if( props.explosiveExpandPerFuel > 0 && parent.GetComp<CompRefuelable>() != null )
			parent.GetComp<CompRefuelable>().ConsumeFuel(parent.GetComp<CompRefuelable>().Fuel);
		
		if( props.destroyThingOnExplosionSize <= radius && !parent.Destroyed )
		{
			destroyedThroughDetonation = true;
			parent.Kill();
		}
		
		// Turn the wick off, in case we survive one way or another
		EndWickSustainer();
		wickStarted = false;

		if( map == null )
		{
			Log.Warning("Tried to detonate CompExplosive in a null map.");
			return;
		}

		if( props.explosionEffect != null )
		{
			var effect = props.explosionEffect.Spawn();
			effect.Trigger(new TargetInfo(parent.PositionHeld, map), new TargetInfo(parent.PositionHeld, map));
			effect.Cleanup();
		}

		GenExplosion.DoExplosion(parent.PositionHeld,
			map,
			radius,
			props.explosiveDamageType,
			instigator ?? parent,
			damAmount: props.damageAmountBase,
			armorPenetration: props.armorPenetrationBase,
			explosionSound: props.explosionSound,
			postExplosionSpawnThingDef: props.postExplosionSpawnThingDef,
			postExplosionSpawnChance: props.postExplosionSpawnChance,
			postExplosionSpawnThingCount: props.postExplosionSpawnThingCount,
			applyDamageToExplosionCellsNeighbors: props.applyDamageToExplosionCellsNeighbors,
			preExplosionSpawnThingDef: props.preExplosionSpawnThingDef,
			preExplosionSpawnChance: props.preExplosionSpawnChance,
			preExplosionSpawnThingCount: props.preExplosionSpawnThingCount,
			chanceToStartFire: props.chanceToStartFire,
			damageFalloff: props.damageFalloff);
	}

	private bool CanExplodeFromDamageType(DamageDef damage)
	{
		return Props.requiredDamageTypeToExplode == null || Props.requiredDamageTypeToExplode == damage;
	}
}}