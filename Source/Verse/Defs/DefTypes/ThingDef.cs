using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text;
using UnityEngine;
using Verse.Sound;
using Verse.AI;
using RimWorld;

namespace Verse
{

public enum FillCategory : byte
{
	None,
	Partial,
	Full,
}

public enum DrawerType : byte
{
	None,
	RealtimeOnly,
	MapMeshOnly,
	MapMeshAndRealTime
}

public enum ResourceCountPriority : byte
{
	Uncounted,

	Last,
	Middle,
	First
}

public enum SurfaceType : byte
{
	None,
	Item,
	Eat
}

public class DamageMultiplier
{
	public DamageDef	damageDef = null;
	public float		multiplier = 1f;
}

public class ThingDef : BuildableDef
{
	//Basics
	public Type						thingClass;
	public ThingCategory 			category;
	public TickerType				tickerType		= TickerType.Never;
	public int						stackLimit		= 1;
	public IntVec2					size			= new IntVec2(1,1);
	public bool						destroyable		= true;
	public bool						rotatable		= true;
	public bool						smallVolume;
	public bool						useHitPoints	= true;
	public bool						receivesSignals;
	public List<CompProperties>		comps			= new List<CompProperties>();

	//Misc
	public List<ThingDefCountClass>	killedLeavings;
	public List<ThingDefCountClass>	butcherProducts;
	public List<ThingDefCountClass>	smeltProducts;
	public bool						smeltable;
	public bool						randomizeRotationOnSpawn;
	public List<DamageMultiplier>	damageMultipliers;
    public bool                     isTechHediff;
	public RecipeMakerProperties	recipeMaker;
	public ThingDef					minifiedDef;
	public bool						isUnfinishedThing;
	public bool						leaveResourcesWhenKilled;
	public ThingDef					slagDef;
	public bool						isFrameInt;
	public IntVec3					interactionCellOffset	= IntVec3.Zero;
    public bool						hasInteractionCell;	
	public ThingDef					interactionCellIcon;
	public bool						interactionCellIconReverse;
	public ThingDef					filthLeaving;
	public bool						forceDebugSpawnable;
	public bool						intricate;
	public bool						scatterableOnMapGen		= true;
	public float					deepCommonality			= 0;
	public int						deepCountPerCell		= 300;
	public int						deepCountPerPortion		= -1;
	public IntRange					deepLumpSizeRange		= IntRange.zero;
	public float					generateCommonality		= 1f;
	public float					generateAllowChance		= 1f;
	private bool					canOverlapZones			= true;
	public FloatRange				startingHpRange			= FloatRange.One;
	[NoTranslate] public List<string> thingSetMakerTags;
	public bool						alwaysFlee;
	public List<RecipeDef>			recipes;

	//Visuals
	public GraphicData				graphicData;
	public DrawerType				drawerType			= DrawerType.RealtimeOnly;
	public bool						drawOffscreen;
	public ColorGenerator			colorGenerator;
	public float					hideAtSnowDepth		= 99999f;
	public bool						drawDamagedOverlay	= true;
	public bool						castEdgeShadows;
	public float					staticSunShadowHeight;

	//Interface
	public bool						selectable;
	public bool 					neverMultiSelect;
	public bool						isAutoAttackableMapObject;
	public bool						hasTooltip;
	public List<Type>				inspectorTabs;
	[Unsaved] public List<InspectTabBase> inspectorTabsResolved;
	public bool						seeThroughFog;
	public bool						drawGUIOverlay;
	public ResourceCountPriority	resourceReadoutPriority		= ResourceCountPriority.Uncounted;
	public bool						resourceReadoutAlwaysShow;
	public bool						drawPlaceWorkersWhileSelected;
	public ConceptDef				storedConceptLearnOpportunity;
	public float					uiIconScale					= 1f;

	//AI hints
	public bool						alwaysHaulable;
	public bool						designateHaulable;
	public List<ThingCategoryDef>	thingCategories;
	public bool						mineable;
	public bool						socialPropernessMatters;
	public bool						stealable = true;

	//Sounds
	public SoundDef					soundDrop;
	public SoundDef					soundPickup;
	public SoundDef					soundInteract;
	public SoundDef					soundImpactDefault;

	//Save/load
    public bool						saveCompressible;
	public bool						isSaveable = true;

	//Physics
	public bool						holdsRoof;
	public float					fillPercent;
	public bool						coversFloor;
	public bool						neverOverlapFloors;
	public SurfaceType				surfaceType = SurfaceType.None;
	public bool						blockPlants;
	public bool						blockLight;
	public bool						blockWind;

	//Trade
	public Tradeability				tradeability = Tradeability.All;
	[NoTranslate] public List<string> tradeTags;
	public bool						tradeNeverStack;
	public ColorGenerator			colorGeneratorInTraderStock;
	
    //Used with equipment or races
    private List<VerbProperties>	verbs = null;
	public List<Tool>				tools;
    
	//Used with equipment/inventory/artificial body parts/implants
    public float                    equippedAngleOffset;
	public EquipmentType			equipmentType	= EquipmentType.None;
	public TechLevel				techLevel		= TechLevel.Undefined;
	[NoTranslate] public List<string> weaponTags;
    [NoTranslate] public List<string> techHediffsTags;
	public bool						destroyOnDrop;
	public List<StatModifier>		equippedStatOffsets;
	
	//Used with blueprints
	public BuildableDef				entityDefToBuild;

	//Used with shells
	public ThingDef					projectileWhenLoaded;

	//Various sub-properties
	public IngestibleProperties		ingestible;
	public FilthProperties			filth;	
	public GasProperties			gas;	
	public BuildingProperties		building;
	public RaceProperties			race;
	public ApparelProperties		apparel;
	public MoteProperties			mote;
	public PlantProperties			plant;
	public ProjectileProperties		projectile;
	public StuffProperties			stuffProps;
	public SkyfallerProperties		skyfaller;

	//Cached
	[Unsaved] private string		descriptionDetailedCached;
	[Unsaved] public Graphic		interactionCellGraphic;

	//Constants
	public const int				SmallUnitPerVolume = 10;
	public const float				SmallVolumePerUnit = 0.1f;


	//======================== Misc properties ==============================
	public bool	EverHaulable{get{return alwaysHaulable || designateHaulable;}}
	public float VolumePerUnit{get{return !smallVolume ? 1 : SmallVolumePerUnit;}}
	public override IntVec2 Size{get{return size;}}
	public bool DiscardOnDestroyed{get{return race == null;}}
	public int	BaseMaxHitPoints	{get{return Mathf.RoundToInt(this.GetStatValueAbstract( StatDefOf.MaxHitPoints ));}}
	public float BaseFlammability	{get{return this.GetStatValueAbstract( StatDefOf.Flammability );}}
	public float BaseMarketValue
	{
		get
		{
			return this.GetStatValueAbstract( StatDefOf.MarketValue );
		}
		set
		{
			this.SetStatBaseValue( StatDefOf.MarketValue, value );
		}
	}
	public float BaseMass
	{
		get
		{
			return this.GetStatValueAbstract( StatDefOf.Mass );
		}
	}
	public bool PlayerAcquirable
	{
		get
		{
			return !destroyOnDrop;
		}
	}
	public bool EverTransmitsPower
	{
		get
		{
			for( int i=0; i<comps.Count; i++ )
			{
				var p = comps[i] as CompProperties_Power;

				if( p != null && p.transmitsPower )
					return true;
			}
			return false;
		}
	}
	public bool Minifiable{get{return minifiedDef != null;}}
	public bool	HasThingIDNumber
	{
		get
		{
			return category != ThingCategory.Mote;
		}
	}
	private List<RecipeDef> allRecipesCached = null;
	public List<RecipeDef> AllRecipes
	{
		get
		{
			if( allRecipesCached == null )
			{
				allRecipesCached = new List<RecipeDef>();
				if( recipes != null )
				{
					for(int i=0; i<recipes.Count; i++ )
					{
						allRecipesCached.Add(recipes[i]);
					}
				}

				var recipeDefs = DefDatabase<RecipeDef>.AllDefsListForReading;
				for( int i=0; i<recipeDefs.Count; i++ )
				{
					if( recipeDefs[i].recipeUsers != null )
					{
						if( recipeDefs[i].recipeUsers.Contains(this) )
							allRecipesCached.Add(recipeDefs[i]);
					}
				}
			}

			return allRecipesCached;
		}
	}
	public bool ConnectToPower
	{
		get
		{
			if( EverTransmitsPower )
				return false;

			for( int i=0; i<comps.Count; i++ )
			{
				if( comps[i].compClass == typeof(CompPowerBattery) )
					return true;

				if( comps[i].compClass == typeof(CompPowerTrader) )
					return true;
			}
			return false;
		}
	}
	public bool CoexistsWithFloors
	{
		get
		{
			return !neverOverlapFloors && !coversFloor;
		}
	}
	public FillCategory Fillage
	{
		get
		{
			if( fillPercent < 0.01f )
				return FillCategory.None;
			else if( fillPercent > 0.99f )
				return FillCategory.Full;
			else
				return FillCategory.Partial;
		}
	}
	public bool MakeFog{get{return Fillage == FillCategory.Full;}}
	public bool CanOverlapZones
	{
		get
		{
			// buildings which support plants can't overlap zones,
			// (so there is no growing zone and a building which supports plants on the same cell)
			if( building != null && building.SupportsPlants )
				return false;

			//Nothing impassable can overlap a zone, except plants
			if( passability == Traversability.Impassable && category != ThingCategory.Plant )
				return false;

			if( surfaceType >= SurfaceType.Item )
				return false;

			if( typeof(ISlotGroupParent).IsAssignableFrom(thingClass) )
				return false;

			if( !canOverlapZones )
				return false;

			//Blueprints and frames inherit from the def they want to build
			if( IsBlueprint || IsFrame )
			{
				var thingDefToBuild = entityDefToBuild as ThingDef;
				if( thingDefToBuild != null )
					return thingDefToBuild.CanOverlapZones;
			}

			return true;
		}
	}
	public bool	CountAsResource{get{return resourceReadoutPriority != ResourceCountPriority.Uncounted;}}
	public bool BlockPlanting
	{
		get
		{
			//Nothing that supports plants blocks planting
			if( building != null && building.SupportsPlants )
				return false;
			
			if( blockPlants )
				return true;

			//All plants block each other
			if( category == ThingCategory.Plant )
				return true;

			if( Fillage > FillCategory.None )
				return true;

			//This includes things like power conduits
			//if( category == EntityCategory.Building )
			//	return true;

			if( this.IsEdifice() )
				return true;

			return false;
		}
	}
	private static List<VerbProperties> EmptyVerbPropertiesList = new List<VerbProperties>();
	public List<VerbProperties> Verbs
	{
		get
		{
			if( verbs != null )
				return verbs;
			return EmptyVerbPropertiesList;
		}
	}
	public bool CanHaveFaction
	{
		get
		{
			if( IsBlueprint || IsFrame )
				return true;

			switch( category )
			{
				case ThingCategory.Pawn: return true;
				case ThingCategory.Building: return true;
			}

			return false;
		}
	}
	public bool Claimable
	{
		get
		{
			return building != null && building.claimable && !building.isNaturalRock;
		}
	}
	public ThingCategoryDef FirstThingCategory
	{
		get
		{
			if( thingCategories.NullOrEmpty() )
				return null;

			return thingCategories[0];
		}
	}
	public float MedicineTendXpGainFactor
	{
		get
		{
			return Mathf.Clamp( this.GetStatValueAbstract(StatDefOf.MedicalPotency)*0.7f, SkillTuning.XpPerTendFactor_NoMedicine, 1.0f );
		}
	}
	public bool CanEverDeteriorate
	{
		get
		{
			if( !useHitPoints )
				return false;

			return category == ThingCategory.Item || this == ThingDefOf.BurnedTree;
		}
	}
	public bool CanInteractThroughCorners
	{
		get
		{
			//We can ALWAYS touch roof holders via corners,
			//this is so we can repair or construct wall corners from inside the corner,
			//the only exception are natural rocks and smoothed rocks -> we don't want to always allow mining diagonally

			if( category != ThingCategory.Building )
				return false;
			
			if( !holdsRoof )
				return false;
			
			if( building != null && building.isNaturalRock && !IsSmoothed )
				return false;
			
			return true;
		}
	}
	/// <summary>
	/// Returns true if this thing affects regions (e.g. is a wall), i.e. whether the regions should be rebuilt whenever this thing is spawned or despawned.
	/// </summary>
	public bool AffectsRegions
	{
		get
		{
			// see RegionTypeUtility.GetExpectedRegionType()
			return passability == Traversability.Impassable || IsDoor;
		}
	}
	/// <summary>
	/// Returns true if this thing affects reachability (e.g. is a wall), i.e. whether the reachability cache should be cleared whenever this thing is spawned or despawned.
	/// </summary>
	public bool AffectsReachability
	{
		get
		{
			// see TouchPathEndModeUtility.IsCornerTouchAllowed()

			//Things which affect regions always affect reachability
			if( AffectsRegions )
				return true;

			if( passability == Traversability.Impassable || IsDoor )
				return true;

			//Makes occupied cells reachable diagonally
			if( TouchPathEndModeUtility.MakesOccupiedCellsAlwaysReachableDiagonally(this) )
				return true;

			return false;
		}
	}

	public string DescriptionDetailed
	{
		get
		{
			if( descriptionDetailedCached == null )
			{
				var sb = new StringBuilder();
				sb.AppendLine(description);
				
				if( IsApparel )
				{
					// Add apparel info
					sb.AppendLine();
					sb.AppendLine(string.Format("{0}: {1}", "Layer".Translate(), apparel.GetLayersString()));
					sb.AppendLine(string.Format("{0}: {1}", "Covers".Translate(), apparel.GetCoveredOuterPartsString(BodyDefOf.Human)));
					if( equippedStatOffsets != null && equippedStatOffsets.Count > 0 )
					{
						sb.AppendLine();
						foreach( var stat in equippedStatOffsets )
						{
							sb.AppendLine(string.Format("{0}: {1}", stat.stat.LabelCap, stat.ValueToStringAsOffset));
						}
					}
				}

				descriptionDetailedCached = sb.ToString();
			}

			return descriptionDetailedCached;
		}
	}

	//Properties: IsKindOfThing bools
	public bool IsApparel{get{return apparel != null;}}
	public bool IsBed { get{return typeof(Building_Bed).IsAssignableFrom(thingClass);} }
	public bool IsCorpse { get{return typeof(Corpse).IsAssignableFrom(thingClass);} }
	public bool IsFrame { get{return isFrameInt;}}
	public bool IsBlueprint { get{return entityDefToBuild != null && category == ThingCategory.Ethereal;}}
	public bool IsStuff				{get{return stuffProps != null;}}
	public bool IsMedicine { get{return statBases.StatListContains(StatDefOf.MedicalPotency);}}
	public bool IsDoor{get{return typeof(Building_Door).IsAssignableFrom(thingClass);}}
	public bool IsFilth{get{return filth != null;}}
	public bool IsIngestible{get{return ingestible != null;}}
	public bool IsNutritionGivingIngestible{get{return IsIngestible && ingestible.CachedNutrition > 0;}}
	public bool IsWeapon { get { return category == ThingCategory.Item && (!verbs.NullOrEmpty() || !tools.NullOrEmpty()); } }
	public bool IsCommsConsole{get{return typeof(Building_CommsConsole).IsAssignableFrom(thingClass);}}
	public bool IsOrbitalTradeBeacon{get{return typeof(Building_OrbitalTradeBeacon).IsAssignableFrom(thingClass);}}
	public bool IsFoodDispenser{get{return typeof(Building_NutrientPasteDispenser).IsAssignableFrom(thingClass);}}
	public bool IsDrug{get{return ingestible != null && ingestible.drugCategory != DrugCategory.None;}}
	public bool IsPleasureDrug{get{return IsDrug && ingestible.joy > 0;}}
	public bool IsNonMedicalDrug{get{return IsDrug && ingestible.drugCategory != DrugCategory.Medical;}}
	public bool IsTable{get{return surfaceType == SurfaceType.Eat && HasComp(typeof(CompGatherSpot));}}
	public bool IsWorkTable{get{return typeof(Building_WorkTable).IsAssignableFrom(thingClass);}}
	public bool IsShell{get{return projectileWhenLoaded != null;}}
	public bool IsArt{get{return IsWithinCategory(ThingCategoryDefOf.BuildingsArt);}}
	public bool IsSmoothable{get{return building != null && building.smoothedThing != null;}}
	public bool IsSmoothed{get{return building != null && building.unsmoothedThing != null;}}
	public bool IsMetal{get{return stuffProps != null && stuffProps.categories.Contains(StuffCategoryDefOf.Metallic);}}
	public bool IsAddictiveDrug
	{
		get
		{
			var compDrug = GetCompProperties<CompProperties_Drug>();
			return compDrug != null && compDrug.addictiveness > 0;
		}
	}
	public bool IsMeat
	{
		get
		{
			return category == ThingCategory.Item
				&& thingCategories != null
				&& thingCategories.Contains(ThingCategoryDefOf.MeatRaw);
		}
	}
	public bool IsLeather
	{
		get
		{
			return category == ThingCategory.Item
				&& thingCategories != null
				&& thingCategories.Contains(ThingCategoryDefOf.Leathers);
		}
	}
	public bool IsRangedWeapon
	{
		get
		{
			if( !IsWeapon )
				return false;
			
			if( !verbs.NullOrEmpty() )
			{
				for( int i = 0; i < verbs.Count; i++ )
				{
					if( !verbs[i].IsMeleeAttack )
						return true;
				}
			}

			return false;
		}
	}
	public bool IsMeleeWeapon
	{
		get
		{
			return IsWeapon && !IsRangedWeapon;
		}
	}
	public bool IsWeaponUsingProjectiles
	{
		get
		{
			if( !IsWeapon )
				return false;
			
			if( !verbs.NullOrEmpty() )
			{
				for( int i = 0; i < verbs.Count; i++ )
				{
					if( verbs[i].LaunchesProjectile )
						return true;
				}
			}

			return false;
		}
	}
	public bool IsBuildingArtificial
	{
		get
		{
			// check for frame to handle special case: floor frames are not buildings
			return (category == ThingCategory.Building || IsFrame)
				&& !(building != null && (building.isNaturalRock || building.isResourceRock));
		}
	}
	
	public bool	EverStorable(bool willMinifyIfPossible)
	{
		//Minified things are always storable
		if( typeof(MinifiedThing).IsAssignableFrom(thingClass) )
			return true;

		if( !thingCategories.NullOrEmpty() )
		{
			//Storable item
			if( category == ThingCategory.Item )
				return true;

			//Can be minified
			if( willMinifyIfPossible && Minifiable )
				return true;
		}
		
		return false;
	}

	private Dictionary<ThingDef, Thing> concreteExamplesInt;
	public Thing GetConcreteExample(ThingDef stuff = null) // this method is used for non-debug purposes, which is a hack
	{
		if( concreteExamplesInt == null )
			concreteExamplesInt = new Dictionary<ThingDef, Thing>();

		if( stuff == null )
			stuff = ThingDefOf.Steel;
		
		if( !concreteExamplesInt.ContainsKey(stuff) )
		{
			if( this.race == null )
				concreteExamplesInt[stuff] = ThingMaker.MakeThing(this, MadeFromStuff ? stuff : null);	// We can't store null keys in a dictionary, so we store null stuff under "steel", then pass the right parameter in here.
			else
				concreteExamplesInt[stuff] = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.AllDefsListForReading.Where(pkd => pkd.race == this).FirstOrDefault());
		}

		return concreteExamplesInt[stuff];
	}
	
	//========================== Comp stuff ================================

	public CompProperties CompDefFor<T>() where T:ThingComp
	{
		return comps.FirstOrDefault( c=> c.compClass == typeof(T) );
	}

	public CompProperties CompDefForAssignableFrom<T>() where T:ThingComp
	{
		return comps.FirstOrDefault( c=> typeof(T).IsAssignableFrom(c.compClass) );
	}

	public bool HasComp(Type compType)
	{
		for( int i = 0; i < comps.Count; i++ )
		{
			if( comps[i].compClass == compType )
				return true;
		}
		return false;
	}

	public T GetCompProperties<T>() where T : CompProperties
	{
		for( int i = 0; i < comps.Count; i++ )
		{
			var c = comps[i] as T;

			if( c != null )
				return c;
		}

		return null;
	}

	//========================== Loading and resolving ================================

	public override void PostLoad()
	{
		if( graphicData != null )
		{
			LongEventHandler.ExecuteWhenFinished(() =>
				{
					if( graphicData.shaderType == null )
						graphicData.shaderType = ShaderTypeDefOf.Cutout;

					graphic = graphicData.Graphic;
				});
		}

		//Assign tools ids
		if( tools != null )
		{
			for( int i = 0; i < tools.Count; i++ )
			{
				tools[i].id = i.ToString();
			}
		}

		//Hack: verb inherits my label
		if( verbs != null && verbs.Count == 1 )
			verbs[0].label = label;

		base.PostLoad();

		//Avoid null refs on things that didn't have a building properties defined
		if( category == ThingCategory.Building && building == null )
			building = new BuildingProperties();

		if( building != null )
			building.PostLoadSpecial(this);

		if( plant != null )
			plant.PostLoadSpecial(this);
	}

	protected override void ResolveIcon()
	{
		base.ResolveIcon();
		
		if( category == ThingCategory.Pawn )
		{
			if (!race.Humanlike)
			{
				var pawnKind = race.AnyPawnKind;
				if (pawnKind != null)
				{
					var bodyMat = pawnKind.lifeStages.Last().bodyGraphicData.Graphic.MatAt(Rot4.East);
					uiIcon = (Texture2D)bodyMat.mainTexture;
					uiIconColor = bodyMat.color;
				}
			}
			else
			{
				//No UI icons for humanlikes because they use a special renderer
			}
		}
		else
		{
			//Resolve color
			var stuff = GenStuff.DefaultStuffFor(this);
			if( colorGenerator != null && (stuff == null || stuff.stuffProps.allowColorGenerators) )
				uiIconColor = colorGenerator.ExemplaryColor;
			else if( stuff != null )
				uiIconColor = stuff.stuffProps.color;
			else if( graphicData != null )
				uiIconColor = graphicData.color;

			//DrawMatSingle always faces the camera, so we sometimes need to rotate it (e.g. if it's Graphic_Single)
			if( rotatable
				&& graphic != null
				&& graphic != BaseContent.BadGraphic
				&& graphic.ShouldDrawRotated
				&& defaultPlacingRot == Rot4.South )
			{
				uiIconAngle = 180f + graphic.DrawRotatedExtraAngleOffset;
			}
		}
	}

	public override void ResolveReferences()
	{
		base.ResolveReferences();

		if( ingestible != null )
			ingestible.parent = this;

		if( building != null )
			building.ResolveReferencesSpecial();

		if( graphicData != null )
			graphicData.ResolveReferencesSpecial();

		if( race != null )
			race.ResolveReferencesSpecial();

		if( stuffProps != null )
			stuffProps.ResolveReferencesSpecial();

		//Default sounds
		if( soundImpactDefault == null )
			soundImpactDefault = SoundDefOf.BulletImpact_Ground;	
		if( soundDrop == null )
			soundDrop = SoundDefOf.Standard_Drop;
		if( soundPickup == null )
			soundPickup = SoundDefOf.Standard_Pickup;
		if( soundInteract == null )
			soundInteract = SoundDefOf.Standard_Pickup;

		//Resolve itabs
		if( inspectorTabs != null && inspectorTabs.Any() )
		{
			inspectorTabsResolved = new List<InspectTabBase>();

			for( int i = 0; i < inspectorTabs.Count; i++ )
			{
				try
				{
					inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(inspectorTabs[i]));
				}
				catch( Exception e )
				{
					Log.Error("Could not instantiate inspector tab of type " + inspectorTabs[i] + ": " + e);
				}
			}
		}

        if (comps != null)
        {
            for (int i = 0; i < comps.Count; i++)
            {
                comps[i].ResolveReferences(this);
            }
        }
	}


	public override IEnumerable<string> ConfigErrors()
	{
		foreach( string str in base.ConfigErrors() )
		{
			yield return str;
		}

		if( label.NullOrEmpty() )
			yield return "no label";

		if( graphicData != null )
		{
			foreach( var err in graphicData.ConfigErrors(this) )
			{
				yield return err;
			}
		}

		if( projectile != null )
		{
			foreach( var err in projectile.ConfigErrors(this) )
			{
				yield return err;
			}
		}
		
		if( statBases != null )
		{
			foreach( var statBase in statBases )
			{
				if( statBases.Where( st=>st.stat == statBase.stat ).Count() > 1 )
					yield return "defines the stat base " + statBase.stat + " more than once.";
			}
		}
		
		if( !BeautyUtility.BeautyRelevant(category) && this.StatBaseDefined(StatDefOf.Beauty) )
			yield return "Beauty stat base is defined, but Things of category " + category + " cannot have beauty.";

		if( char.IsNumber(defName[defName.Length-1]) )
			yield return "ends with a numerical digit, which is not allowed on ThingDefs.";

		if( thingClass == null )
			yield return "has null thingClass.";

		if( comps.Count > 0 && !typeof(ThingWithComps).IsAssignableFrom( thingClass ) )	
			yield return "has components but it's thingClass is not a ThingWithComps";

		if( ConnectToPower && drawerType == DrawerType.RealtimeOnly && IsFrame )
			yield return "connects to power but does not add to map mesh. Will not create wire meshes.";

		if( costList != null )
		{
			foreach( ThingDefCountClass cost in costList )
			{
				if( cost.count == 0 )
					yield return "cost in " + cost.thingDef + " is zero.";
			}
		}

		if( thingCategories != null )
		{
			var doubleCat = thingCategories.FirstOrDefault( cat=>thingCategories.Count(c=>c==cat) > 1 );
			if( doubleCat != null )
				yield return "has duplicate thingCategory " + doubleCat + ".";
		}

		if( Fillage == FillCategory.Full && category != ThingCategory.Building )
			yield return "gives full cover but is not a building.";

		if( comps.Any(c=>c.compClass == typeof(CompPowerTrader) ) && drawerType == DrawerType.MapMeshOnly )
			yield return "has PowerTrader comp but does not draw real time. It won't draw a needs-power overlay.";
	
		if( equipmentType != EquipmentType.None )
		{
			if( techLevel == TechLevel.Undefined )
				yield return "is equipment but has no tech level.";

			if( !comps.Any(c=>c.compClass == typeof(CompEquippable) ) )
				yield return "is equipment but has no CompEquippable";
		}

		if( thingClass == typeof(Bullet) && projectile.damageDef == null )
			yield return " is a bullet but has no damageDef.";

		if( destroyOnDrop )
		{
			if( !menuHidden )
				yield return "destroyOnDrop but not menuHidden.";

			if( tradeability != Tradeability.None )
				yield return "destroyOnDrop but tradeability is " + tradeability;
		}

		if( stackLimit > 1 && !drawGUIOverlay )
			yield return "has stackLimit > 1 but also has drawGUIOverlay = false.";

		if( damageMultipliers != null )
		{
			foreach( DamageMultiplier mult in damageMultipliers )
			{
				if( damageMultipliers.Where( m=>m.damageDef == mult.damageDef ).Count() > 1 )
				{
					yield return "has multiple damage multipliers for damageDef " + mult.damageDef;
					break;
				}
			}
		}

		if( Fillage == FillCategory.Full && !this.IsEdifice() )
			yield return "fillPercent is 1.00 but is not edifice";

		if( MadeFromStuff && constructEffect != null )
			yield return "madeFromStuff but has a defined constructEffect (which will always be overridden by stuff's construct animation).";

		if( MadeFromStuff && stuffCategories.NullOrEmpty() )
			yield return "madeFromStuff but has no stuffCategories.";

		if( costList.NullOrEmpty() && costStuffCount <= 0 && recipeMaker != null )
			yield return "has a recipeMaker but no costList or costStuffCount.";

		if( this.GetStatValueAbstract( StatDefOf.DeteriorationRate ) > 0.00001f && !CanEverDeteriorate )
			yield return "has >0 DeteriorationRate but can't deteriorate.";

		if( drawerType == DrawerType.MapMeshOnly && comps.Any( c=>c.compClass == typeof(CompForbiddable) ) )
			yield return "drawerType=MapMeshOnly but has a CompForbiddable, which must draw in real time.";

		if( smeltProducts != null && smeltable )
			yield return "has smeltProducts but has smeltable=false";

		if( equipmentType != EquipmentType.None && verbs.NullOrEmpty() && tools.NullOrEmpty() )
			yield return "is equipment but has no verbs or tools";

		if( Minifiable && thingCategories.NullOrEmpty() )
			yield return "is minifiable but not in any thing category";

		if( category == ThingCategory.Building && !Minifiable && !thingCategories.NullOrEmpty() )
			yield return "is not minifiable yet has thing categories (could be confusing in thing filters because it can't be moved/stored anyway)";
		
		if( this != ThingDefOf.MinifiedThing &&
			(EverHaulable || Minifiable) &&
			(statBases.NullOrEmpty() || !statBases.Any(s => s.stat == StatDefOf.Mass)) )
			yield return "is haulable, but does not have an authored mass value";

		if( ingestible == null && this.GetStatValueAbstract(StatDefOf.Nutrition) != 0 )
			yield return "has nutrition but ingestible properties are null";

		if( BaseFlammability != 0f && !useHitPoints && category != ThingCategory.Pawn )
			yield return "flammable but has no hitpoints (will burn indefinitely)";

		if( graphicData != null && graphicData.shadowData != null  )
		{
			//This works fine in some cases
			//if( castEdgeShadows )
			//	yield return "graphicData defines a shadowInfo but castEdgeShadows is also true";

			if( staticSunShadowHeight > 0)
				yield return "graphicData defines a shadowInfo but staticSunShadowHeight > 0";
		}

		if( saveCompressible && Claimable )
			yield return "claimable item is compressible; faction will be unset after load";
		
		if( deepCommonality > 0 != deepLumpSizeRange.TrueMax > 0 )
			yield return "if deepCommonality or deepLumpSizeRange is set, the other also must be set";

		if( deepCommonality > 0 && deepCountPerPortion <= 0 )
			yield return "deepCommonality > 0 but deepCountPerPortion is not set";

		if( verbs != null )
		{
			for( int i = 0; i < verbs.Count; i++ )
			{
				foreach( var err in verbs[i].ConfigErrors(this) )
				{
					yield return string.Format("verb {0}: {1}", i, err);
				}
			}
		}

		if( race != null && tools != null )
		{
			for( int i=0; i<tools.Count; i++ )
			{
				if( tools[i].linkedBodyPartsGroup != null && !race.body.AllParts.Any(part=>part.groups.Contains(tools[i].linkedBodyPartsGroup) ) )
				{
					yield return "has tool with linkedBodyPartsGroup " + tools[i].linkedBodyPartsGroup + " but body " + race.body + " has no parts with that group.";
				}
			}
		}

		if( building != null )
		{
			foreach( var err in building.ConfigErrors(this) )
			{
				yield return err;
			}
		}

		if( apparel != null )
		{
			foreach( var err in apparel.ConfigErrors(this) )
			{
				yield return err;
			}
		}

		if( comps != null )
		{
			for( int i=0; i<comps.Count; i++ )
			{
				foreach( var err in comps[i].ConfigErrors(this) )
				{
					yield return err;
				}
			}
		}

		if( race != null )
		{
			foreach( var e in race.ConfigErrors() )
			{
				yield return e;
			}
		}

		if( ingestible != null )
		{
			foreach( var e in ingestible.ConfigErrors() )
			{
				yield return e;
			}
		}

		if( plant != null )
		{
			foreach( var e in plant.ConfigErrors() )
			{
				yield return e;
			}
		}

		if( tools != null )
		{
			var dupeTool = tools.SelectMany(lhs => tools.Where(rhs => lhs != rhs && lhs.id == rhs.id)).FirstOrDefault();
			if( dupeTool != null )
				yield return string.Format("duplicate thingdef tool id {0}", dupeTool.id);

			foreach( var t in tools )
			{
				foreach( var e in t.ConfigErrors() )
				{
					yield return e;
				}
			}
		}
	}

	public static ThingDef Named(string defName)
	{
		return DefDatabase<ThingDef>.GetNamed(defName);
	}
        
    //========================== Misc ================================

    public string LabelAsStuff
    {
        get
        {
            if (!stuffProps.stuffAdjective.NullOrEmpty())
            {
                return stuffProps.stuffAdjective;
            }
            else
            {
                return label;
            }
        }
    }

	public bool IsWithinCategory(ThingCategoryDef category)
	{
		if( thingCategories == null )
			return false;

		for( int i = 0; i < thingCategories.Count; ++i )
		{
			var cur = thingCategories[i];
			while( cur != null )
			{
				if( cur == category )
					return true;
				
				cur = cur.parent;
			}
		}

		return false;
	}

	//===========================================================================
	//=========================== Info card stats ===============================
	//===========================================================================

	public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
    {
		foreach( var stat in base.SpecialDisplayStats(req) )
		{
			yield return stat;
		}

		if(apparel != null)
        {
            string coveredParts = apparel.GetCoveredOuterPartsString(BodyDefOf.Human);
            yield return new StatDrawEntry( StatCategoryDefOf.Apparel, "Covers".Translate(), coveredParts, 100);

			yield return new StatDrawEntry( StatCategoryDefOf.Apparel, "Layer".Translate(), apparel.GetLayersString(), 95);
        }

		if( IsMedicine && MedicineTendXpGainFactor != 1f )
			yield return new StatDrawEntry(StatCategoryDefOf.Basics, "MedicineXpGainFactor".Translate(), MedicineTendXpGainFactor.ToStringPercent());

		if( fillPercent > 0 && fillPercent < 1.0f
			&& (category == ThingCategory.Item || category == ThingCategory.Building || category == ThingCategory.Plant) )
		{
			var sde = new StatDrawEntry(StatCategoryDefOf.Basics, "CoverEffectiveness".Translate(), this.BaseBlockChance().ToStringPercent());
			sde.overrideReportText = "CoverEffectivenessExplanation".Translate();
			yield return sde;
		}

		if( constructionSkillPrerequisite > 0 )
			yield return new StatDrawEntry(StatCategoryDefOf.Basics, "ConstructionSkillRequired".Translate(), constructionSkillPrerequisite.ToString(), overrideReportText: "ConstructionSkillRequiredExplanation".Translate());

        if( !verbs.NullOrEmpty() )
        {
            var verb = verbs.First(x => x.isPrimary);

			//Verbs can be native verbs held by pawns, or weapon verbs
			StatCategoryDef verbStatCategory = category == ThingCategory.Pawn
				? verbStatCategory = StatCategoryDefOf.PawnCombat
				: verbStatCategory = StatCategoryDefOf.Weapon;

			float warmup = verb.warmupTime;
			if( warmup > 0 )
			{
				var warmupLabel = category == ThingCategory.Pawn ? "MeleeWarmupTime".Translate() : "WarmupTime".Translate();
				yield return new StatDrawEntry( verbStatCategory, warmupLabel, warmup.ToString("0.##") + " s", 40);
			}

			//NOTE: this won't work with custom projectiles, e.g. mortars
            if(verb.defaultProjectile != null)
			{
				var damageAmountExplanation = new StringBuilder();
				float dam = verb.defaultProjectile.projectile.GetDamageAmount(req.Thing, damageAmountExplanation);
				yield return new StatDrawEntry(verbStatCategory, "Damage".Translate(), dam.ToString(), 50, damageAmountExplanation.ToString());

				if( verb.defaultProjectile.projectile.damageDef.armorCategory != null )
				{
					var armorPenetrationExplanation = new StringBuilder();
					float ap = verb.defaultProjectile.projectile.GetArmorPenetration(req.Thing, armorPenetrationExplanation);
					var fullExplanation = "ArmorPenetrationExplanation".Translate();
					if( armorPenetrationExplanation.Length != 0 )
						fullExplanation += "\n\n" + armorPenetrationExplanation;
					yield return new StatDrawEntry(verbStatCategory, "ArmorPenetration".Translate(), ap.ToStringPercent(), 49, fullExplanation );
				}
			}
  
            if(verb.LaunchesProjectile)
            {
                int burstShotCount = verb.burstShotCount;
                float burstShotFireRate = 60f / verb.ticksBetweenBurstShots.TicksToSeconds();
                float range = verb.range;
                    
                if(burstShotCount > 1)
                {
                    yield return new StatDrawEntry( verbStatCategory, "BurstShotCount".Translate(), burstShotCount.ToString(), 20);
                    yield return new StatDrawEntry( verbStatCategory, "BurstShotFireRate".Translate(),
                                                                burstShotFireRate.ToString("0.##") + " rpm", 19);
                }

				//We round range to the nearest whole number; we don't want to show the ".9"'s we use to avoid weird-shaped
				//max-range circles
                yield return new StatDrawEntry( verbStatCategory, "Range".Translate(), range.ToString("F0"), 10);

				if( verb.defaultProjectile != null && verb.defaultProjectile.projectile != null && verb.defaultProjectile.projectile.stoppingPower != 0f )
					yield return new StatDrawEntry( verbStatCategory, "StoppingPower".Translate(), verb.defaultProjectile.projectile.stoppingPower.ToString("F1"), overrideReportText: "StoppingPowerExplanation".Translate() );
            }

			if( verb.forcedMissRadius > 0f )
			{
				yield return new StatDrawEntry(verbStatCategory, "MissRadius".Translate(), verb.forcedMissRadius.ToString("0.#"), 30);
				yield return new StatDrawEntry(verbStatCategory, "DirectHitChance".Translate(), (1f / GenRadial.NumCellsInRadius(verb.forcedMissRadius)).ToStringPercent(), 29);
			}
        }
            
        if( plant != null )
        {
			foreach( var s in plant.SpecialDisplayStats() )
			{
				yield return s;
			}  
        }
            
        if( ingestible != null )
        {
			foreach( var s in ingestible.SpecialDisplayStats() )
			{
				yield return s;
			}
        }
            
        if( race != null )
        {
			foreach( var s in race.SpecialDisplayStats(this) )
			{
				yield return s;
			}
        }

		if( building != null )
		{
			foreach( var s in building.SpecialDisplayStats(this, req) )
			{
				yield return s;
			}
		}
                   
        if( isTechHediff )
        {
            //We have to iterate through all recipes to see where this body part item or implant is used
            foreach(RecipeDef def in DefDatabase <RecipeDef>.AllDefs.Where(x => x.IsIngredient(this)))
            {
                HediffDef diff = def.addsHediff;
                if(diff != null)
                {
                    if(diff.addedPartProps != null)
                        yield return new StatDrawEntry( StatCategoryDefOf.Basics, "BodyPartEfficiency".Translate(), diff.addedPartProps.partEfficiency.ToStringByStyle(ToStringStyle.PercentZero) );
                        
                    foreach( var s in diff.SpecialDisplayStats(StatRequest.ForEmpty()) )
                    {
                        yield return s;
                    }
                        
					var vg = diff.CompProps<HediffCompProperties_VerbGiver>();
                    if(vg != null )
                    {
						if( !vg.verbs.NullOrEmpty() )
						{
							var verb = vg.verbs[0];

							if( !verb.IsMeleeAttack )
							{
								//NOTE: this won't work with custom projectiles (CompChangeableProjectile)
								if( verb.defaultProjectile != null )
								{
									int projDamage = verb.defaultProjectile.projectile.GetDamageAmount(null);
									yield return new StatDrawEntry(StatCategoryDefOf.Basics, "Damage".Translate(), projDamage.ToString());

									if( verb.defaultProjectile.projectile.damageDef.armorCategory != null )
									{
										float projArmorPenetration = verb.defaultProjectile.projectile.GetArmorPenetration(null);
										yield return new StatDrawEntry(StatCategoryDefOf.Basics, "ArmorPenetration".Translate(), projArmorPenetration.ToStringPercent(), overrideReportText: "ArmorPenetrationExplanation".Translate());
									}
								}
							}
							else
							{
								//Damage handled by StatWorker_MeleeAverageDPS

								int meleeDamage = verb.meleeDamageBaseAmount;

								if( verb.meleeDamageDef.armorCategory != null )
								{
									float armorPenetration = verb.meleeArmorPenetrationBase;
									if( armorPenetration < 0f )
										armorPenetration = meleeDamage * VerbProperties.DefaultArmorPenetrationPerDamage;
									yield return new StatDrawEntry(StatCategoryDefOf.Weapon, "ArmorPenetration".Translate(), armorPenetration.ToStringPercent(), overrideReportText: "ArmorPenetrationExplanation".Translate());
								}
							}
						}
						else if( !vg.tools.NullOrEmpty() )
						{
							//Damage handled by StatWorker_MeleeAverageDPS

							var tool = vg.tools[0];
							
							if( ThingUtility.PrimaryMeleeWeaponDamageType(vg.tools).armorCategory != null )
							{
								float armorPenetration = tool.armorPenetration;
								if( armorPenetration < 0f )
									armorPenetration = tool.power * VerbProperties.DefaultArmorPenetrationPerDamage;

								yield return new StatDrawEntry(StatCategoryDefOf.Weapon, "ArmorPenetration".Translate(), armorPenetration.ToStringPercent(), overrideReportText: "ArmorPenetrationExplanation".Translate());
							}
						}
                    }

                    // now, check thought defs
                    var thought = DefDatabase<ThoughtDef>.AllDefs.FirstOrDefault(x => x.hediff == diff);

                    if(thought != null && thought.stages != null && thought.stages.Any())
                        yield return new StatDrawEntry(StatCategoryDefOf.Basics, "MoodChange".Translate(), (thought.stages.First().baseMoodEffect).ToStringByStyle(ToStringStyle.Integer, ToStringNumberSense.Offset));
                }
            }
        }

		for( int i = 0; i < comps.Count; i++ )
		{
			foreach( var s in comps[i].SpecialDisplayStats(req) )
			{
				yield return s;
			}
		}
    }

}
}











