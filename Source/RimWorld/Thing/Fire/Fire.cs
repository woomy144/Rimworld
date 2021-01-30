using UnityEngine;
using UnityEngine.Profiling;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;




namespace RimWorld{
public class Fire : AttachableThing, ISizeReporter
{
	//Working vars - gameplay
	private int				ticksSinceSpawn;
	public float			fireSize = MinFireSize; //1 is a medium-sized campfire
	private int				ticksSinceSpread; //Unsaved, unimportant
	private float			flammabilityMax = 0.5f; //Updated only periodically


	//Working vars - audiovisual
	private int				ticksUntilSmoke = 0;
	private Sustainer		sustainer = null;

	//Working vars - static
	private static List<Thing> flammableList = new List<Thing>();
	private static int		fireCount;
	private static int		lastFireCountUpdateTick;


	//Constants
	public const float		MinFireSize = 0.1f;
	private const float		MinSizeForSpark = 1f;
	private const float		TicksBetweenSparksBase = 150; //Halves for every fire size
	private const float		TicksBetweenSparksReductionPerFireSize = 40;
	private const float		MinTicksBetweenSparks = 75;
	private const float		MinFireSizeToEmitSpark = 1f;
	public const float		MaxFireSize = 1.75f;
	private const int		TicksToBurnFloor = GenDate.TicksPerHour * 3;

    private const int       ComplexCalcsInterval = 150;

	private const float		CellIgniteChancePerTickPerSize = 0.01f;
	private const float		MinSizeForIgniteMovables = 0.4f;

	private const float		FireBaseGrowthPerTick = 0.00055f;

	private static readonly IntRange SmokeIntervalRange = new IntRange(130,200);
	private const int		SmokeIntervalRandomAddon = 10;

	private const float		BaseSkyExtinguishChance = 0.04f;
	private const int		BaseSkyExtinguishDamage = 10;

	private const float		HeatPerFireSizePerInterval = 160f;
	private const float		HeatFactorWhenDoorPresent = 0.15f;

	private const float		SnowClearRadiusPerFireSize = 3f;
	private const float		SnowClearDepthFactor = 0.1f;

	private const int		FireCountParticlesOff = 15;

	//Properties
	public override string Label
	{
		get
		{
			if( parent != null )
				return "FireOn".Translate(parent.LabelCap, parent);	
			else
				return this.def.label;
		}
	}
	public override string InspectStringAddon
	{
		get
		{
			return "Burning".Translate() + " (" + "FireSizeLower".Translate( (fireSize*100).ToString("F0") ) + ")";	
		}
	}
	private float SpreadInterval
	{
		get
		{
			float ticks = TicksBetweenSparksBase - (fireSize-1)*TicksBetweenSparksReductionPerFireSize;

			if( ticks < MinTicksBetweenSparks )
				ticks = MinTicksBetweenSparks;

			return ticks;
		}
	}



	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref ticksSinceSpawn, "ticksSinceSpawn");
		Scribe_Values.Look(ref fireSize, "fireSize");
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);
		RecalcPathsOnAndAroundMe(map);
		LessonAutoActivator.TeachOpportunity(ConceptDefOf.HomeArea, this, OpportunityType.Important );

		ticksSinceSpread = (int)(SpreadInterval * Rand.Value);
	}

	public float CurrentSize()
	{
		return fireSize;
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		if( sustainer != null )
		{
			if( sustainer.externalParams.sizeAggregator == null )
				sustainer.externalParams.sizeAggregator = new SoundSizeAggregator();
			sustainer.externalParams.sizeAggregator.RemoveReporter(this);
		}

		var map = Map; // before despawning

		base.DeSpawn(mode);

		//Recalculate paths after despawning
		RecalcPathsOnAndAroundMe(map);
	}

	private void RecalcPathsOnAndAroundMe(Map map)
	{
		var adj = GenAdj.AdjacentCellsAndInside;

		for( int i = 0; i < adj.Length; i++ )
		{
			var c = Position + adj[i];

			if( !c.InBounds(map) )
				continue;

			map.pathGrid.RecalculatePerceivedPathCostAt(c);
		}
	}

	public override void AttachTo(Thing parent)
	{
		base.AttachTo(parent);

		Pawn p = parent as Pawn;
		if( p != null )
		{
			TaleRecorder.RecordTale( TaleDefOf.WasOnFire, p );
		}
	}
	
	public override void Tick()
	{
		//Update ticks
		ticksSinceSpawn++;

		//Update fire count
		if( lastFireCountUpdateTick != Find.TickManager.TicksGame )
		{
			fireCount = Map.listerThings.ThingsOfDef(def).Count;
			lastFireCountUpdateTick = Find.TickManager.TicksGame;
		}

		//Maintain sustainer
		if( sustainer != null )
			sustainer.Maintain();
		else
		{
			// Do we need to make a sustainer?
			if( !Position.Fogged(Map) )
			{
				// Yes. Yes we do.

				// we use the current cell instead of "this" as the target, because this sustainer can outlive this Fire instance
				// (because we use AggregateOrSpawnSustainerFor so something else can use it) 
				var info = SoundInfo.InMap(new TargetInfo(Position, Map), MaintenanceType.PerTick);

				sustainer = SustainerAggregatorUtility.AggregateOrSpawnSustainerFor(this, SoundDefOf.FireBurning, info);
			}
		}
		// Since "re-fogging" isn't currently a thing, we don't bother dealing with fires that are now invisible

		//Spawn particles
        Profiler.BeginSample("Spawn particles");
		{
			//Do smoke and glow
			ticksUntilSmoke--;
			if( ticksUntilSmoke <= 0 )
				SpawnSmokeParticles();

			//Do visual micro sparks
			if( fireCount < FireCountParticlesOff && fireSize > 0.7f && Rand.Value < fireSize * 0.01f )
				MoteMaker.ThrowMicroSparks(DrawPos, Map);
		}
		Profiler.EndSample(); //Spawn particles

		//Spread fire
		Profiler.BeginSample("Spread");
		if( fireSize > MinSizeForSpark )
		{
			ticksSinceSpread++;
			if( ticksSinceSpread >= SpreadInterval )
			{
				TrySpread();
				ticksSinceSpread = 0;
			}
		}
		Profiler.EndSample(); //Spread

		//Do complex calcs
		if( Gen.IsHashIntervalTick(this, ComplexCalcsInterval) )
			DoComplexCalcs();

		if( ticksSinceSpawn >= TicksToBurnFloor )
			TryBurnFloor();
	}

	private void SpawnSmokeParticles()
	{
		if( fireCount < FireCountParticlesOff )
			MoteMaker.ThrowSmoke(DrawPos, Map, fireSize);

		if( fireSize > 0.5f && parent == null )
			MoteMaker.ThrowFireGlow(Position, Map, fireSize);

		float firePct = fireSize / 2;
		if( firePct > 1 )
			firePct = 1;
		firePct = 1f - firePct;
		ticksUntilSmoke = SmokeIntervalRange.Lerped(firePct) + (int)(SmokeIntervalRandomAddon * Rand.Value);
	}

	private void DoComplexCalcs()
	{
		bool cellContainsDoor = false;

		Profiler.BeginSample("Determine flammability");
		//Determine list of flammables in my cell
		flammableList.Clear();
		flammabilityMax = 0;
		if( Position.GetTerrain(Map).extinguishesFire )
		{
			//  immediately extinguish; we just leave flammabilityMax at 0
		}
		else if( parent == null )
		{
			if( Position.TerrainFlammableNow(Map) )
				flammabilityMax = Position.GetTerrain(Map).GetStatValueAbstract(StatDefOf.Flammability);

			var cellThings = Map.thingGrid.ThingsListAt(Position);
			for( int i = 0; i < cellThings.Count; i++ )
			{
				var ct = cellThings[i];
				if( ct is Building_Door )
					cellContainsDoor = true;

				var thingFlam = ct.GetStatValue(StatDefOf.Flammability);

				if( thingFlam < 0.01f )
					continue;

				//Record its flammability
				flammableList.Add(cellThings[i]);
				if( thingFlam > flammabilityMax )
					flammabilityMax = thingFlam;

				//If I'm a static fire and it's a pawn and I'm big, ignite it
				if( parent == null && fireSize > MinSizeForIgniteMovables && cellThings[i].def.category == ThingCategory.Pawn )
					cellThings[i].TryAttachFire(fireSize * 0.2f);
			}
		}
		else
		{
			//Consider only my parent
			flammableList.Add(parent);
			flammabilityMax = parent.GetStatValue(StatDefOf.Flammability);
		}
		Profiler.EndSample(); //Determine flammability

		//Destroy me if I have nothing to burn
		if( flammabilityMax < 0.01f )
		{
			Destroy();
			return;
		}

		Profiler.BeginSample("Do damage");
		{
			//Choose what I'm going to damage
			Thing damagee;
			if( parent != null )
				damagee = parent;						//Damage parent
			else if( flammableList.Count > 0 )
				damagee = flammableList.RandomElement();//Damage random flammable thing in cell
			else
				damagee = null;

			//Damage whatever we're supposed to damage
			if( damagee != null )
			{
				//We don't damage the target if it's not our parent, it would attach a fire, and we're too small
				//This is to avoid tiny fires igniting passing pawns
				if( !(fireSize < MinSizeForIgniteMovables && damagee != parent && damagee.def.category == ThingCategory.Pawn) )
					DoFireDamage(damagee);
			}
		}
		Profiler.EndSample(); //Do damage

		//If still spawned (after doing damage)...
		if( Spawned )
		{
			Profiler.BeginSample("Room heat");
			//Push some heat
			float fireEnergy = fireSize * HeatPerFireSizePerInterval;
			//Hack to reduce impact on doors, otherwise they hit insane temperatures fast
			if( cellContainsDoor )
				fireEnergy *= HeatFactorWhenDoorPresent;
			GenTemperature.PushHeat(Position, Map, fireEnergy);
			Profiler.EndSample(); //Room heat

			Profiler.BeginSample("Snow clear");
			if( Rand.Value < 0.4f )
			{
				//Clear some snow around the fire
				float snowClearRadius = fireSize * SnowClearRadiusPerFireSize;
				SnowUtility.AddSnowRadial(Position, Map, snowClearRadius, -(fireSize * SnowClearDepthFactor));
			}
			Profiler.EndSample(); //Snow clear


			Profiler.BeginSample("Grow/extinguish");
			//Try to grow the fire
			fireSize += FireBaseGrowthPerTick
					  * flammabilityMax
					  * ComplexCalcsInterval;

			if( fireSize > MaxFireSize )
				fireSize = MaxFireSize;

			//Extinguish from sky (rain etc)
			if( Map.weatherManager.RainRate > 0.01f )
			{
				if( VulnerableToRain() )
				{
					if( Rand.Value < BaseSkyExtinguishChance * ComplexCalcsInterval )
						TakeDamage(new DamageInfo(DamageDefOf.Extinguish, BaseSkyExtinguishDamage));
				}
			}
			Profiler.EndSample(); //Grow/extinguish
		}
	}

	private void TryBurnFloor()
	{
		if( parent != null || !Spawned )
			return;

		if( Position.TerrainFlammableNow(Map) )
			Map.terrainGrid.Notify_TerrainBurned(Position);
	}

	private bool VulnerableToRain()
	{
		if( !Spawned )
			return false;

		var roof = Map.roofGrid.RoofAt(Position);

		//Always affected under no roof
		if( roof == null )
			return true;

		//Never affected under thick roof
		if( roof.isThickRoof )
			return false;

		//Affected under thin roof if and only if a roof holder is here (meaning that I am a wall being rained on).
		Thing building = Position.GetEdifice(Map);
		return building != null && building.def.holdsRoof;
	}

	private void DoFireDamage( Thing targ )
	{
		float damPerTick = 0.0125f + (0.0036f * fireSize);
		damPerTick = Mathf.Clamp( damPerTick, 0.0125f, 0.05f );
		int damAmount = GenMath.RoundRandom(damPerTick * ComplexCalcsInterval);
		if( damAmount < 1 )
			damAmount = 1;

		Pawn p = targ as Pawn;
		if( p != null )
		{
			BattleLogEntry_DamageTaken log = new BattleLogEntry_DamageTaken(p, RulePackDefOf.DamageEvent_Fire);
			Find.BattleLog.Add(log);

			DamageInfo dinfo = new DamageInfo(DamageDefOf.Flame, damAmount, instigator: this);
			dinfo.SetBodyRegion( depth: BodyPartDepth.Outside );
			targ.TakeDamage(dinfo).AssociateWithLog(log);

			//Damage a random apparel
			if( p.apparel != null )
			{
				Apparel ap;
				if( p.apparel.WornApparel.TryRandomElement(out ap) )
					ap.TakeDamage(new DamageInfo(DamageDefOf.Flame, damAmount, instigator: this));
			}
		}
		else
		{
			targ.TakeDamage(new DamageInfo(DamageDefOf.Flame, damAmount, instigator: this));
		}
	}
	
	protected void TrySpread()
	{
		//This method is optimized as it is a performance bottleneck (as are the sparks it spawns)

		IntVec3 targLoc = Position;
		bool adjacent;
		if( Rand.Chance(0.8f) )
		{
			targLoc = Position + GenRadial.ManualRadialPattern[ Rand.RangeInclusive(1,8) ];	//Spark adjacent
			adjacent = true;
		}
		else
		{
			targLoc = Position + GenRadial.ManualRadialPattern[ Rand.RangeInclusive(10,20) ];	//Spark distant
			adjacent = false;
		}
		
		if( !targLoc.InBounds(Map) )
			return;

		if( Rand.Chance(FireUtility.ChanceToStartFireIn(targLoc, Map)) )
		{
			if( !adjacent )
			{
				var startRect = CellRect.SingleCell(Position);
				var endRect = CellRect.SingleCell(targLoc);

				// don't create a spark if we'll hit a wall in our way
				if( !GenSight.LineOfSight(Position, targLoc, Map, startRect, endRect) )
					return;

				var sp = (Spark)GenSpawn.Spawn(ThingDefOf.Spark, Position, Map);
				sp.Launch(this, targLoc, targLoc, ProjectileHitFlags.All);
			}
			else
			{
				//When adjacent, skip sparks and just magically spawn fires
				FireUtility.TryStartFireIn(targLoc, Map, Fire.MinFireSize);
			}
		}
	}

}}