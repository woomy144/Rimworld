using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

using Verse.Sound;
using Verse.AI;


namespace RimWorld
{

public enum PlantLifeStage : byte
{
	Sowing,
	Growing,
	Mature,
}

[StaticConstructorOnStartup]
public class Plant : ThingWithComps
{
	//Working vars
	protected float 					growthInt = BaseGrowthPercent; //Start in growing phase by default, set to 0 if sowing
	protected int						ageInt = 0;
	protected int						unlitTicks = 0;
	protected int						madeLeaflessTick = -99999;
	public bool							sown = false;

	//Working vars - cache
	private string						cachedLabelMouseover = null;

	//Fast vars
	private static Color32[]			workingColors = new Color32[4];

	//Constants and content
	public const float					BaseGrowthPercent = 0.05f;
	private const float					BaseDyingDamagePerTick = 1f/200f;
	private static readonly FloatRange	DyingDamagePerTickBecauseExposedToLight = new FloatRange(0.02f/200f, 0.2f/200f);
	private const float					GridPosRandomnessFactor = 0.30f;
	private const int					TicksWithoutLightBeforeStartDying = (int)(GenDate.TicksPerDay * 7.5f);
	private const int					LeaflessMinRecoveryTicks = 60000;	//Minimum time to not show leafless after being made leafless
    public const float					MinGrowthTemperature = 0;			//Min temperature at which plant can grow or reproduce
    public const float					MinOptimalGrowthTemperature = 10f;
    public const float					MaxOptimalGrowthTemperature = 42f;
    public const float					MaxGrowthTemperature = 58;			//Max temperature at which plant can grow or reproduce
	public const float					MaxLeaflessTemperature = -2;
	private const float					MinLeaflessTemperature = -10;
	private const float					MinAnimalEatPlantsTemperature = 0;
	public const float					TopVerticesAltitudeBias = 0.1f;
	private static Graphic				GraphicSowing = GraphicDatabase.Get<Graphic_Single>("Things/Plant/Plant_Sowing", ShaderDatabase.Cutout, Vector2.one, Color.white);

	[TweakValue("Graphics", -1, 1)]	private static float	LeafSpawnRadius = 0.4f;
	[TweakValue("Graphics", 0, 2)] 	private static float	LeafSpawnYMin = 0.3f;
	[TweakValue("Graphics", 0, 2)] 	private static float	LeafSpawnYMax = 1.0f;

	//Properties
	public virtual float Growth
	{
		get{ return growthInt; }
		set
		{
			growthInt = Mathf.Clamp01(value);
			cachedLabelMouseover = null;
		}
	}
	public virtual int Age
	{
		get{ return ageInt; }
		set
		{
			ageInt = value;
			cachedLabelMouseover = null;
		}
	}
	public virtual bool HarvestableNow
	{
		get
		{
			return def.plant.Harvestable && growthInt > def.plant.harvestMinGrowth;
		}
	}
	public bool HarvestableSoon
	{
		get
		{
			if( HarvestableNow )
				return true;

			if( !def.plant.Harvestable )
				return false;
			
			float leftGrowth = Mathf.Max(1f - Growth, 0f);
			float leftDays = leftGrowth * def.plant.growDays;
			float leftGrowthAny = Mathf.Max(1f - def.plant.harvestMinGrowth, 0f);
			float leftDaysAny = leftGrowthAny * def.plant.growDays;

			return (leftDays <= 10f || leftDaysAny <= 1f)
				&& GrowthRateFactor_Fertility > 0f
				&& GrowthRateFactor_Temperature > 0f;
		}
	}
	public virtual bool BlightableNow
	{
		get
		{
			return !Blighted
				&& def.plant.Blightable
				&& sown
				&& LifeStage != PlantLifeStage.Sowing;
		}
	}
	public Blight Blight
	{
		get
		{
			if( !Spawned || !def.plant.Blightable )
				return null;

			return Position.GetFirstBlight(Map);
		}
	}
	public bool Blighted
	{
		get
		{
			return Blight != null;
		}
	}
	public override bool IngestibleNow
	{
		get
		{
			if( !base.IngestibleNow )
				return false;

			//Trees are always edible
			// This allows alphabeavers completely destroy the tree ecosystem
			if( def.plant.IsTree )
				return true;

			if( growthInt < def.plant.harvestMinGrowth )
				return false;

			if( LeaflessNow )
				return false;

			if( Spawned && Position.GetSnowDepth(Map) > def.hideAtSnowDepth )
				return false;

			return true;
		}
	}
	public virtual float CurrentDyingDamagePerTick
	{
		get
		{
			if( !Spawned )
				return 0f;

			float damage = 0f;

			if( def.plant.LimitedLifespan && ageInt > def.plant.LifespanTicks )
				damage = Mathf.Max(damage, BaseDyingDamagePerTick);

			if( !def.plant.cavePlant && unlitTicks > TicksWithoutLightBeforeStartDying )
				damage = Mathf.Max(damage, BaseDyingDamagePerTick);

			if( DyingBecauseExposedToLight )
			{
				float glow = Map.glowGrid.GameGlowAt(Position, ignoreCavePlants: true);
				damage = Mathf.Max(damage, DyingDamagePerTickBecauseExposedToLight.LerpThroughRange(glow));
			}

			return damage;
		}
	}
	public virtual bool DyingBecauseExposedToLight
	{
		get
		{
			return def.plant.cavePlant
				&& Spawned
				&& Map.glowGrid.GameGlowAt(Position, ignoreCavePlants: true) > 0f;
		}
	}
	public bool Dying { get { return CurrentDyingDamagePerTick > 0f; } }
	protected virtual bool Resting
	{
		get
		{
			return GenLocalDate.DayPercent(this) < 0.25f || GenLocalDate.DayPercent(this) > 0.8f;
		}
	}
	public virtual float GrowthRate
	{
		get
		{
			if( Blighted )
				return 0f;

			if( Spawned && !PlantUtility.GrowthSeasonNow(Position, Map) )
				return 0f;

			return GrowthRateFactor_Fertility * GrowthRateFactor_Temperature * GrowthRateFactor_Light;
		}
	}
	protected float GrowthPerTick
	{
		get
		{
			if( LifeStage != PlantLifeStage.Growing || Resting )
				return 0;

			float baseRate = 1 / (GenDate.TicksPerDay * def.plant.growDays);
			return baseRate * GrowthRate;
		}
	}
	public float GrowthRateFactor_Fertility
	{
		get
		{
			return (Map.fertilityGrid.FertilityAt(Position) * def.plant.fertilitySensitivity)
				 + (1f-def.plant.fertilitySensitivity);
		}
	}
	public float GrowthRateFactor_Light
	{
		get
		{
			float glow = Map.glowGrid.GameGlowAt(Position);

			//Edge case: if min glow is exactly the same as optimal glow, and current glow is exactly the same, then we choose 100% (a choice between 0% and 100%)
			if( def.plant.growMinGlow == def.plant.growOptimalGlow && glow == def.plant.growOptimalGlow )
				return 1f;
			
			return GenMath.InverseLerp( def.plant.growMinGlow, def.plant.growOptimalGlow, glow);
		}
	}
	public float GrowthRateFactor_Temperature
    {
        get
        {
            float cellTemp;
			if( !GenTemperature.TryGetTemperatureForCell(Position, Map, out cellTemp) )
				return 1;

            if (cellTemp < MinOptimalGrowthTemperature)
				return Mathf.InverseLerp( MinGrowthTemperature, MinOptimalGrowthTemperature, cellTemp );
            else if (cellTemp > MaxOptimalGrowthTemperature)
				return Mathf.InverseLerp( MaxGrowthTemperature, MaxOptimalGrowthTemperature, cellTemp );
			else
				return 1;
        }
    }
	protected int TicksUntilFullyGrown
	{
		get
		{
			if( growthInt > 0.9999f )
				return 0;

			float growthPerTick = GrowthPerTick;

			if( growthPerTick == 0f )
				return int.MaxValue;
			else
				return (int)((1f - growthInt) / growthPerTick);
		}
	}
	protected string GrowthPercentString
	{
		get
		{
			return (growthInt + 0.0001f).ToStringPercent();
		}
	}
	public override string LabelMouseover
	{
		get
		{
			if( cachedLabelMouseover == null )
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(def.LabelCap);
				sb.Append(" (" + "PercentGrowth".Translate(GrowthPercentString));
			
				if( Dying )
					sb.Append(", " + "DyingLower".Translate() );
			
				sb.Append(")");
				cachedLabelMouseover = sb.ToString();
			}

			return cachedLabelMouseover;
		}
	}
	protected virtual bool HasEnoughLightToGrow
	{
		get
		{
			return GrowthRateFactor_Light > 0.001f;
		}
	}
	public virtual PlantLifeStage LifeStage
	{
		get
		{
			if( growthInt < 0.001f )
				return PlantLifeStage.Sowing;

			if( growthInt > 0.999f )
				return PlantLifeStage.Mature;

			return PlantLifeStage.Growing;
		}
	}
	public override Graphic Graphic
	{
		get
		{
			if( LifeStage == PlantLifeStage.Sowing )
				return GraphicSowing;

			//Note: Plants that you sowed and which are harvestable now never display the leafless graphic
			if( def.plant.leaflessGraphic != null && LeaflessNow && !(sown && HarvestableNow) )
				return def.plant.leaflessGraphic;
			
			if( def.plant.immatureGraphic != null && !HarvestableNow )
				return def.plant.immatureGraphic;

			return base.Graphic;
		}
	}
	public bool LeaflessNow
	{
		get
		{
			if( Find.TickManager.TicksGame - madeLeaflessTick < LeaflessMinRecoveryTicks )
				return true;
			else
				return false;
		}
	}
	protected virtual float LeaflessTemperatureThresh
	{
		get
		{
			float diff = MaxLeaflessTemperature - MinLeaflessTemperature;
			float leaflessThresh = ((this.HashOffset() * 0.01f) % diff) - diff + MaxLeaflessTemperature;

			return leaflessThresh;
		}
	}
	public bool IsCrop
	{
		get
		{
			if( !def.plant.Sowable )
				return false;

			if( !Spawned )
			{
				Log.Warning("Can't determine if crop when unspawned.");
				return false;
			}

			return def == WorkGiver_Grower.CalculateWantedPlantDef(Position, Map);
		}
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);

		//Don't call during init because indoor warm plants will all go leafless if it's cold outside
		if( Current.ProgramState == ProgramState.Playing && !respawningAfterLoad )
			CheckTemperatureMakeLeafless();
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		var blight = Position.GetFirstBlight(Map);

		base.DeSpawn(mode);

		if( blight != null )
			blight.Notify_PlantDeSpawned();
	}

	public override void ExposeData()
	{
		base.ExposeData();
		
		Scribe_Values.Look(ref growthInt, 	"growth");
		Scribe_Values.Look(ref ageInt, 		"age", 0);
		Scribe_Values.Look(ref unlitTicks,	"unlitTicks", 0 );
		Scribe_Values.Look(ref madeLeaflessTick, "madeLeaflessTick", -99999);
		Scribe_Values.Look(ref sown,		"sown", false);
	}

	public override void PostMapInit()
	{
		CheckTemperatureMakeLeafless();
	}

	protected override void IngestedCalculateAmounts(Pawn ingester, float nutritionWanted, out int numTaken, out float nutritionIngested)
	{
		float nut = this.GetStatValue(StatDefOf.Nutrition);

		//Affect this thing
		if( def.plant.HarvestDestroys )
			numTaken = 1;
		else
		{
			growthInt -= 0.30f;

			if( growthInt < 0.08f )
				growthInt = 0.08f;

			if( Spawned )
				Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things);

			numTaken = 0;
		}

		nutritionIngested = nut;
	}

	public virtual void PlantCollected()
	{
		if( def.plant.HarvestDestroys )
			Destroy();
		else
		{
			growthInt = def.plant.harvestAfterGrowth;
			Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things);
		}
	}
	
	protected virtual void CheckTemperatureMakeLeafless()
	{
		if( AmbientTemperature < LeaflessTemperatureThresh )
			MakeLeafless( LeaflessCause.Cold );
	}

	public enum LeaflessCause{Cold, Poison};
	public virtual void MakeLeafless(LeaflessCause cause)
	{
		bool changed = !LeaflessNow;
		var map = Map; // before applying damage

		if( cause == LeaflessCause.Poison && def.plant.leaflessGraphic == null )
		{
			//Poisoned a plant without a leafless graphic - we have to kill it

			if( IsCrop )
			{
				if( MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfPoison-"+def.defName, 240f) )
					Messages.Message( "MessagePlantDiedOfPoison".Translate(GetCustomLabelNoCount(includeHp: false)).CapitalizeFirst(), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
			}

			TakeDamage(new DamageInfo(DamageDefOf.Rotting, 99999));	
		}
		else if( def.plant.dieIfLeafless )
		{
			//Plant dies if ever leafless

			if( IsCrop )
			{
				if( cause == LeaflessCause.Cold )
				{
					if( MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfCold-"+def.defName, 240f) )
						Messages.Message( "MessagePlantDiedOfCold".Translate(GetCustomLabelNoCount(includeHp: false)).CapitalizeFirst(), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
				}
				else if ( cause == LeaflessCause.Poison )
				{
					if( MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfPoison-"+def.defName, 240f) )
						Messages.Message( "MessagePlantDiedOfPoison".Translate(GetCustomLabelNoCount(includeHp: false)).CapitalizeFirst(), new TargetInfo(Position, map), MessageTypeDefOf.NegativeEvent );
				}
			}

			TakeDamage(new DamageInfo(DamageDefOf.Rotting, 99999));	
		}
		else
		{
			//Just become visually leafless
			madeLeaflessTick = Find.TickManager.TicksGame;
		}

		if( changed )
			map.mapDrawer.MapMeshDirty( Position, MapMeshFlag.Things );
	}

	public override void TickLong()
	{
		CheckTemperatureMakeLeafless();

		if( Destroyed )
			return;

		if( PlantUtility.GrowthSeasonNow(Position, Map) )
		{
			//Grow
			float prevGrowth = growthInt;
			bool wasMature = LifeStage == PlantLifeStage.Mature;
			growthInt += GrowthPerTick * GenTicks.TickLongInterval;

			if( growthInt > 1f )
				growthInt = 1f;

			//Regenerate layers
			if( (!wasMature && LifeStage == PlantLifeStage.Mature)
				|| (int)(prevGrowth * 10f) != (int)(growthInt * 10f) )
			{
				if( CurrentlyCultivated() )
					Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Things);
			}
		}

		bool hasLight = HasEnoughLightToGrow;

		//Record light starvation
		if( !hasLight )
			unlitTicks += GenTicks.TickLongInterval;
		else
			unlitTicks = 0;

		//Age
		ageInt += GenTicks.TickLongInterval;

		//Dying
		if( Dying )
		{
			var map = Map;
			bool isCrop = IsCrop; // before applying damage!
			bool harvestableNow = HarvestableNow;
			bool dyingBecauseExposedToLight = DyingBecauseExposedToLight;

			int dyingDamAmount = Mathf.CeilToInt(CurrentDyingDamagePerTick * GenTicks.TickLongInterval);
			TakeDamage(new DamageInfo(DamageDefOf.Rotting, dyingDamAmount));

			if( Destroyed )
			{
				if( isCrop && def.plant.Harvestable && MessagesRepeatAvoider.MessageShowAllowed("MessagePlantDiedOfRot-" + def.defName, 240f) )
				{
					string messageKey;
					if( harvestableNow )
						messageKey = "MessagePlantDiedOfRot_LeftUnharvested";
					else if( dyingBecauseExposedToLight )
						messageKey = "MessagePlantDiedOfRot_ExposedToLight";
					else
						messageKey = "MessagePlantDiedOfRot";

					Messages.Message(messageKey.Translate(GetCustomLabelNoCount(includeHp: false)).CapitalizeFirst(),
						new TargetInfo(Position, map),
						MessageTypeDefOf.NegativeEvent);
				}

				return;
			}
		}

		//State has changed, label may have to as well
		//Also, we want to keep this null so we don't have useless data sitting there a long time in plants that never get looked at
		cachedLabelMouseover = null;

		// Drop a leaf
		if( def.plant.dropLeaves )
		{
			var mote = MoteMaker.MakeStaticMote(Vector3.zero, Map, ThingDefOf.Mote_Leaf) as MoteLeaf;
			if( mote != null )
			{
				float size = def.plant.visualSizeRange.LerpThroughRange(growthInt);
				float graphicSize = def.graphicData.drawSize.x * size; //Plants don't support non-square drawSizes

				var disc = Rand.InsideUnitCircleVec3 * LeafSpawnRadius;	// Horizontal alignment
				mote.Initialize(Position.ToVector3Shifted()	// Center of the tile
						+ Vector3.up * Rand.Range(LeafSpawnYMin, LeafSpawnYMax)	// Vertical alignment
						+ disc	// Horizontal alignment
						+ Vector3.forward * def.graphicData.shadowData.offset.z,	// Move to the approximate base of the tree
					Rand.Value * GenTicks.TickLongInterval.TicksToSeconds(),
					disc.z > 0,
					graphicSize
				);
			}
		}
	}

	protected virtual bool CurrentlyCultivated()
	{
		if( !def.plant.Sowable )
			return false;

		if( !Spawned )
			return false;
		
		var z = Map.zoneManager.ZoneAt(Position);
		if (z != null && z is Zone_Growing)
			return true;

		var ed = Position.GetEdifice(Map);
		if( ed != null && ed.def.building.SupportsPlants )
			return true;

		return false;
	}

	public virtual bool CanYieldNow()
	{
		if( !HarvestableNow )
			return false;

		//If yield is 0, handle it
		if( def.plant.harvestYield <= 0 )
			return false;

		if( Blighted )
			return false;
		
		return true;
	}

	public virtual int YieldNow()
	{
		if( !CanYieldNow() )
			return 0;

		//Start with max yield
		float yieldFloat = def.plant.harvestYield;

		//Factor for growth
		float growthFactor = Mathf.InverseLerp( def.plant.harvestMinGrowth, 1, growthInt);
		growthFactor = 0.5f + growthFactor * 0.5f;	//Scalebias it to 0.5..1 range.
		yieldFloat *= growthFactor;

		//Factor down for health with a 50% lerp factor
		yieldFloat *=  Mathf.Lerp( 0.5f, 1f,  ((float)HitPoints / (float)MaxHitPoints) );

		//Factor for difficulty
		yieldFloat *= Find.Storyteller.difficulty.cropYieldFactor;

		return GenMath.RoundRandom(yieldFloat);		
	}

	public override void Print( SectionLayer layer )
	{
		Vector3 trueCenter = this.TrueCenter();

		Rand.PushState();
		Rand.Seed = Position.GetHashCode();//So our random generator makes the same numbers every time

		//Determine how many meshes to print
		int meshCount = Mathf.CeilToInt(growthInt * def.plant.maxMeshCount);
		if( meshCount < 1 )
			meshCount = 1;

		//Determine plane size
		float size = def.plant.visualSizeRange.LerpThroughRange(growthInt);
		float graphicSize = def.graphicData.drawSize.x * size; //Plants don't support non-square drawSizes

		//Shuffle up the position indices and place meshes at them
		//We do this to get even mesh placement by placing them roughly on a grid
		Vector3 adjustedCenter = Vector3.zero;
		int meshesYielded = 0;
		var posIndexList = PlantPosIndices.GetPositionIndices(this);
		bool clampedBottomToCellBottom = false;
		for(int i=0; i<posIndexList.Length; i++ )
		{		
			int posIndex = posIndexList[i];

			//Determine center position
			if( def.plant.maxMeshCount == 1 )
			{
				//Determine random local position variance
				const float PositionVariance = 0.05f;

				adjustedCenter = trueCenter + Gen.RandomHorizontalVector(PositionVariance);

				//Clamp bottom of plant to square bottom
				//So tall plants grow upward
				float squareBottom = Position.z;
				if( adjustedCenter.z - size / 2f < squareBottom )
				{
					adjustedCenter.z = squareBottom + size / 2f;
					clampedBottomToCellBottom = true;
				}
			}
			else
			{
				//Grid width is the square root of max mesh count
				int gridWidth = 1;
				switch( def.plant.maxMeshCount )
				{
				case 1: gridWidth = 1; break;
				case 4: gridWidth = 2; break;
				case 9: gridWidth = 3; break;
				case 16: gridWidth = 4; break;
				case 25: gridWidth = 5; break;
				default: Log.Error(def + " must have plant.MaxMeshCount that is a perfect square."); break;
				}
				float gridSpacing = 1f / gridWidth; //This works out to give half-spacings around the edges

				adjustedCenter = Position.ToVector3(); //unshifted
				adjustedCenter.y = def.Altitude;//Set altitude

				//Place this mesh at its randomized position on the submesh grid
				adjustedCenter.x += 0.5f * gridSpacing;
				adjustedCenter.z += 0.5f * gridSpacing;
				int xInd = posIndex / gridWidth;
				int zInd = posIndex % gridWidth;
				adjustedCenter.x += xInd * gridSpacing;
				adjustedCenter.z += zInd * gridSpacing;
				
				//Add a random offset
				float gridPosRandomness = gridSpacing * GridPosRandomnessFactor;
				adjustedCenter += Gen.RandomHorizontalVector(gridPosRandomness);
			}

			//Randomize horizontal flip
			bool flipped = Rand.Bool;

			//Randomize material
			var mat = Graphic.MatSingle; //Pulls a random material

			//Set wind exposure value at each vertex by setting vertex color
			PlantUtility.SetWindExposureColors(workingColors, this);

			var planeSize = new Vector2(graphicSize, graphicSize);

			Printer_Plane.PrintPlane( layer, 
									  adjustedCenter, 
									  planeSize,	
									  mat, 
									  flipUv: flipped, 
									  colors:  workingColors,
									  topVerticesAltitudeBias: TopVerticesAltitudeBias,	// need to beat walls corner filler (so trees don't get cut by mountains)
									  uvzPayload: Gen.HashOffset(this) % 1024 );


			meshesYielded++;
			if( meshesYielded >= meshCount )
				break;
		}

		if( def.graphicData.shadowData != null )
		{
			//Start with a standard shadow center
			var shadowCenter = trueCenter + def.graphicData.shadowData.offset * size;

			//Clamp center of shadow to cell bottom
			if( clampedBottomToCellBottom )
				shadowCenter.z = Position.ToVector3Shifted().z + def.graphicData.shadowData.offset.z;

			shadowCenter.y -= Altitudes.AltInc;

			var shadowVolume = def.graphicData.shadowData.volume * size;

			Printer_Shadow.PrintShadow(layer, shadowCenter, shadowVolume, Rot4.North);
		}

		Rand.PopState();
	}

	public override string GetInspectString()
	{
		var sb = new StringBuilder();

		if( LifeStage == PlantLifeStage.Growing )
		{
			sb.AppendLine("PercentGrowth".Translate(GrowthPercentString));
			sb.AppendLine("GrowthRate".Translate() + ": " + GrowthRate.ToStringPercent());

			if( !Blighted )
			{
				if( Resting )
					sb.AppendLine("PlantResting".Translate());

				if( !HasEnoughLightToGrow )
					sb.AppendLine("PlantNeedsLightLevel".Translate() + ": " + def.plant.growMinGlow.ToStringPercent());

				float tempFactor = GrowthRateFactor_Temperature;
				if( tempFactor < 0.99f )
				{
					if( tempFactor < 0.01f )
						sb.AppendLine("OutOfIdealTemperatureRangeNotGrowing".Translate());
					else
						sb.AppendLine("OutOfIdealTemperatureRange".Translate(Mathf.RoundToInt(tempFactor * 100f).ToString()));
				}
			}
		}
		else if( LifeStage == PlantLifeStage.Mature )
		{
			if( HarvestableNow )
				sb.AppendLine("ReadyToHarvest".Translate() );
			else
				sb.AppendLine("Mature".Translate() );
		}

		if( DyingBecauseExposedToLight )
			sb.AppendLine("DyingBecauseExposedToLight".Translate());

		if( Blighted )
			sb.AppendLine("Blighted".Translate() + " (" + Blight.Severity.ToStringPercent() + ")");

        return sb.ToString().TrimEndNewlines();
	}
	
	public virtual void CropBlighted()
	{
		if( !Blighted )
			GenSpawn.Spawn(ThingDefOf.Blight, Position, Map);
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach( var gizmo in base.GetGizmos() )
		{
			yield return gizmo;
		}

		//Spread blight
		if( Prefs.DevMode && Blighted )
		{
			var spread = new Command_Action();
			spread.defaultLabel = "Dev: Spread blight";
			spread.action = () => Blight.TryReproduceNow();
			yield return spread;
		}
	}
}

public static class PlantPosIndices
{
	//Cached
	//First index - for maxMeshCount
	//Second index - which of the lists for this maxMeshCount
	//Third index - which index in this list
	private static int[][][] rootList = null;

	//Constants
	private const int ListCount = 8;

	static PlantPosIndices()
	{
		rootList = new int[PlantProperties.MaxMaxMeshCount][][];
		for( int i=0; i<PlantProperties.MaxMaxMeshCount; i++ )
		{
			rootList[i] = new int[ListCount][];
			for( int j=0; j<ListCount; j++ )
			{
				int[] newList = new int[i+1];
				for( int k=0; k<i; k++ )
				{
					newList[k] = k;
				}
				newList.Shuffle();

				rootList[i][j] = newList;
			}
		}
	}

	public static int[] GetPositionIndices( Plant p )
	{
		int mmc = p.def.plant.maxMeshCount;
		int index = (p.thingIDNumber^42348528) % ListCount;
		return rootList[mmc-1][ index ];
	}
}

}
