using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Verse;
using UnityEngine;


namespace RimWorld{
public class PawnGenOption
{
	//Config
	public PawnKindDef			kind;
	public float 				selectionWeight;

	//Properties
	public float Cost{get{return kind.combatPower;}}

	public override string ToString()
	{
		return "(" + (kind!=null?kind.ToString():"null")
			+ " w=" + selectionWeight.ToString("F2")
			+ " c=" + (kind!=null?Cost.ToString("F2"):"null") + ")";
	}

	public void LoadDataFromXmlCustom( XmlNode xmlRoot )
	{
		DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef( this, "kind", xmlRoot.Name );
		selectionWeight = ParseHelper.FromString<float>( xmlRoot.FirstChild.Value );
	}
}

public class FactionDef : Def
{
	//General config
	public bool					isPlayer = false;
	public RulePackDef			factionNameMaker;
	public RulePackDef			settlementNameMaker;
	public RulePackDef			playerInitialSettlementNameMaker;
	[MustTranslate] public string fixedName = null;
	public bool					humanlikeFaction = true;
	public bool					hidden = false;
	public float				listOrderPriority = 0f;
	public List<PawnGroupMaker>	pawnGroupMakers = null;
	public SimpleCurve			raidCommonalityFromPointsCurve = null;
	public bool					autoFlee = true;
	public bool					canSiege = false;
	public bool					canStageAttacks = false;
	public bool					canUseAvoidGrid = true;
	public float				earliestRaidDays = 0;
	public FloatRange			allowedArrivalTemperatureRange = new FloatRange(-1000, 1000);
	public PawnKindDef			basicMemberKind;
	public List<ResearchProjectTagDef>	startingResearchTags = null;
	[NoTranslate] public List<string>	recipePrerequisiteTags = null;
	public bool					rescueesCanJoin = false;
	[MustTranslate] public string pawnSingular = "member";
	[MustTranslate] public string pawnsPlural = "members";
	public string				leaderTitle = "leader"; //Not MustTranslate because many faction defs never have leaders
	public float				forageabilityFactor = 1f;
	public SimpleCurve			maxPawnCostPerTotalPointsCurve = null;

	//Faction generation
	public int					requiredCountAtGameStart = 0;
	public int					maxCountAtGameStart = 9999;
	public bool					canMakeRandomly = false;
	public float				settlementGenerationWeight = 0f;

	//Humanlike faction config
	public RulePackDef			pawnNameMaker;
	public TechLevel			techLevel = TechLevel.Undefined;
	[NoTranslate] public List<string> backstoryCategories = null;
	[NoTranslate] public List<string> hairTags = new List<string>();
	public ThingFilter			apparelStuffFilter = null;
	public List<TraderKindDef>	caravanTraderKinds = new List<TraderKindDef>();
	public List<TraderKindDef>	visitorTraderKinds = new List<TraderKindDef>();
	public List<TraderKindDef>	baseTraderKinds = new List<TraderKindDef>();
	public float				geneticVariance = 1f;

	//Relations (can apply to non-humanlike factions)
	public IntRange				startingGoodwill = IntRange.zero;
	public bool					mustStartOneEnemy = false;
	public IntRange				naturalColonyGoodwill = IntRange.zero;
	public float				goodwillDailyGain = 0;
	public float				goodwillDailyFall = 0;
	public bool					permanentEnemy = false;

	//World drawing
	[NoTranslate] public string	homeIconPath;
	[NoTranslate] public string	expandingIconTexture;
	public List<Color>			colorSpectrum;

	//Unsaved
	[Unsaved] private Texture2D	expandingIconTextureInt;

	//Properties
	public bool CanEverBeNonHostile
	{
		get
		{
			return !permanentEnemy;
		}
	}
	public Texture2D ExpandingIconTexture
	{
		get
		{
			if( expandingIconTextureInt == null )
			{
				if( !expandingIconTexture.NullOrEmpty() )
					expandingIconTextureInt = ContentFinder<Texture2D>.Get(expandingIconTexture);
				else
					expandingIconTextureInt = BaseContent.BadTex;
			}

			return expandingIconTextureInt;
		}
	}

	public float MinPointsToGeneratePawnGroup(PawnGroupKindDef groupKind)
	{
		if( pawnGroupMakers == null )
			return 0;

		var groups = pawnGroupMakers.Where(x => x.kindDef == groupKind);

		if( !groups.Any() )
			return 0;

		return groups.Min(pgm => pgm.MinPointsToGenerateAnything);
	}

	public bool CanUseStuffForApparel( ThingDef stuffDef )
	{
		if( apparelStuffFilter == null )
			return true;

		return apparelStuffFilter.Allows( stuffDef );
	}

	public float RaidCommonalityFromPoints( float points )
	{
		if( points < 0 || raidCommonalityFromPointsCurve == null )
			return 1f;

		return raidCommonalityFromPointsCurve.Evaluate(points);
	}

	public override void ResolveReferences()
	{
		base.ResolveReferences();

		if( apparelStuffFilter != null )
			apparelStuffFilter.ResolveReferences();
	}

	public override IEnumerable<string> ConfigErrors()
	{
		foreach( var error in base.ConfigErrors() )
			yield return error;

		if( pawnGroupMakers != null && maxPawnCostPerTotalPointsCurve == null )
			yield return "has pawnGroupMakers but missing maxPawnCostPerTotalPointsCurve";

		if( !isPlayer && factionNameMaker == null && fixedName == null )
			yield return "FactionTypeDef " + defName + " lacks a factionNameMaker and a fixedName.";

		if( techLevel == TechLevel.Undefined )
			yield return defName + " has no tech level.";

		if( humanlikeFaction )
		{
			if( backstoryCategories.NullOrEmpty() )
				yield return defName + " is humanlikeFaction but has no backstory categories.";

			if( hairTags.Count == 0 )
				yield return defName + " is humanlikeFaction but has no hairTags.";
		}

		if( isPlayer )
		{
			if( settlementNameMaker == null )
				yield return "isPlayer is true but settlementNameMaker is null";

			if( factionNameMaker == null )
				yield return "isPlayer is true but factionNameMaker is null";
			
			if( playerInitialSettlementNameMaker == null )
				yield return "isPlayer is true but playerInitialSettlementNameMaker is null";
		}

		if( permanentEnemy )
		{
			if( mustStartOneEnemy )
				yield return "permanentEnemy has mustStartOneEnemy = true, which is redundant";

			if( goodwillDailyFall != 0 || goodwillDailyGain != 0 )
				yield return "permanentEnemy has a goodwillDailyFall or goodwillDailyGain";

			if( startingGoodwill != IntRange.zero )
				yield return "permanentEnemy has a startingGoodwill defined";

			if (naturalColonyGoodwill != IntRange.zero )
				yield return "permanentEnemy has a naturalColonyGoodwill defined";
		}
	}

	public static FactionDef Named( string defName )
	{
		return DefDatabase<FactionDef>.GetNamed(defName);
	}
}}

