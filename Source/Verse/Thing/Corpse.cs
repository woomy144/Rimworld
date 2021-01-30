using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;

namespace Verse
{

public class Corpse : ThingWithComps, IThingHolder, IThoughtGiver, IStrippable, IBillGiver
{
	//Config
	private ThingOwner<Pawn> innerContainer;

	//Working vars
	public int timeOfDeath = -1;
    private int vanishAfterTimestamp = -1;
    private BillStack operationsBillStack = null;
	public bool everBuriedInSarcophagus;
    
    //Constants
	private const int VanishAfterTicksSinceDessicated = 100 * GenDate.TicksPerDay;

	//Properties
	public Pawn InnerPawn
	{
		get
		{
			if( innerContainer.Count > 0 )
				return innerContainer[0];
			else
				return null;
		}
		set
		{
			if( value == null )
				innerContainer.Clear();
			else
			{
				if( innerContainer.Count > 0 )
				{
					Log.Error("Setting InnerPawn in corpse that already has one.");
					innerContainer.Clear();
				}

				innerContainer.TryAdd(value);
			}
		}
	}
    public int Age
	{
        get
        {
            return Find.TickManager.TicksGame - timeOfDeath;
        }
        set
        {
            timeOfDeath = Find.TickManager.TicksGame - value;
        }
    }
	public override string LabelNoCount
	{
		get
		{
			if( Bugged )
			{
				Log.ErrorOnce("Corpse.Label while Bugged", 57361644);
				return "";
			}
			return "DeadLabel".Translate(InnerPawn.Label, InnerPawn);
		}
	}
	public override bool IngestibleNow
	{
		get
		{
			if( Bugged )
			{
				Log.Error("IngestibleNow on Corpse while Bugged.");
				return false;
			}

			if( !base.IngestibleNow )
				return false;

			if( !InnerPawn.RaceProps.IsFlesh )
				return false;

			if( this.GetRotStage() != RotStage.Fresh )
				return false;

			return true;
		}
	}
	public RotDrawMode CurRotDrawMode
	{
		get
		{
			var rottable = GetComp<CompRottable>();

			if( rottable != null )
			{
				if( rottable.Stage == RotStage.Rotting )
					return RotDrawMode.Rotting;
				else if( rottable.Stage == RotStage.Dessicated )
					return RotDrawMode.Dessicated;
			}

			return RotDrawMode.Fresh;
		}
	}
    private bool ShouldVanish
    {
        get
        {
             return InnerPawn.RaceProps.Animal &&
                    vanishAfterTimestamp > 0 &&
                    Age >= vanishAfterTimestamp &&
					Spawned &&
                    (this.GetRoom() != null && this.GetRoom().TouchesMapEdge) &&
                    !Map.roofGrid.Roofed(Position);
        }
    }
    public BillStack BillStack { get { return operationsBillStack; } }
    public IEnumerable <IntVec3> IngredientStackCells { get { yield return InteractionCell; } }
    public bool Bugged
	{
		get
		{
			//This shouldn't ever happen and is purely a bug mitigation
			return innerContainer.Count == 0
				|| innerContainer[0] == null
				|| innerContainer[0].def == null
				|| innerContainer[0].kindDef == null;
		}
	}


    public Corpse()
    {
        operationsBillStack = new BillStack(this);
		innerContainer = new ThingOwner<Pawn>(this, oneStackOnly: true, contentsLookMode: LookMode.Reference);
    }
    
    public bool CurrentlyUsableForBills()
    {
        return InteractionCell.IsValid;
    }

	public bool UsableForBillsAfterFueling()
    {
		return CurrentlyUsableForBills();
	}
    
    public bool AnythingToStrip()
    {
        return InnerPawn.AnythingToStrip();
    }

	public ThingOwner GetDirectlyHeldThings()
	{
		return innerContainer;
	}

	public void GetChildHolders(List<IThingHolder> outChildren)
	{
		ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
	}

	public override void PostMake()
	{
		base.PostMake();

		timeOfDeath = Find.TickManager.TicksGame;
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		if( Bugged )
		{
			Log.Error(this + " spawned in bugged state.");
			return;
		}

		base.SpawnSetup(map, respawningAfterLoad);

		InnerPawn.Rotation = Rot4.South; //Fixes drawing errors

		NotifyColonistBar();
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		base.DeSpawn(mode);

		if( !Bugged )
			NotifyColonistBar();
	}

	public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
	{
		Pawn innerPawn = null;
		if( !Bugged )
		{
			innerPawn = InnerPawn; // store the reference before removing him from the container so we can use it later
			NotifyColonistBar();
			innerContainer.Clear();
		}

		base.Destroy(mode);

		if( innerPawn != null )
			Corpse.PostCorpseDestroy(innerPawn);
	}

	public static void PostCorpseDestroy(Pawn pawn)
	{
		// unclaim grave if we have any
		if( pawn.ownership != null )
			pawn.ownership.UnclaimAll();

		// destroy equipment
		if( pawn.equipment != null )
			pawn.equipment.DestroyAllEquipment();

		// destroy inventory
		pawn.inventory.DestroyAll();

		// destroy apparel
		if( pawn.apparel != null )
			pawn.apparel.DestroyAll();
	}

    public override void TickRare()
    {
		base.TickRare();

		if( Destroyed )
			return; // in case we rot away when ticking base

		if( Bugged )
		{
			Log.Error(this + " has null innerPawn. Destroying.");
			Destroy();
			return;
		}

		InnerPawn.TickRare();

        // reset vanishAfterTimestamp to X days from now if not previously set, or if carcass still fresh
        if (vanishAfterTimestamp < 0 || this.GetRotStage() != RotStage.Dessicated)
			vanishAfterTimestamp = Age + VanishAfterTicksSinceDessicated;

        if(ShouldVanish)
            Destroy();
    }

	protected override void IngestedCalculateAmounts(Pawn ingester, float nutritionWanted, out int numTaken, out float nutritionIngested)
	{
		//Determine part to take
		var part = GetBestBodyPartToEat(ingester, nutritionWanted);
		if( part == null )
		{
			Log.Error(ingester + " ate " + this + " but no body part was found. Replacing with core part.");
			part = InnerPawn.RaceProps.body.corePart;
		}

		//Determine the nutrition to gain
		float nut = FoodUtility.GetBodyPartNutrition(this, part);

		//Affect this thing
		//If ate core part, remove the whole corpse
		//Otherwise, remove the eaten body part
		if( part == InnerPawn.RaceProps.body.corePart )
		{
			if( PawnUtility.ShouldSendNotificationAbout(InnerPawn) && InnerPawn.RaceProps.Humanlike )
				Messages.Message("MessageEatenByPredator".Translate(InnerPawn.LabelShort, ingester.Named("PREDATOR"), InnerPawn.Named("EATEN")).CapitalizeFirst(), ingester, MessageTypeDefOf.NegativeEvent);

			numTaken = 1;
		}
		else
		{
			var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, InnerPawn, part);
			missing.lastInjury = HediffDefOf.Bite;
			missing.IsFresh = true;
			InnerPawn.health.AddHediff(missing);

			numTaken = 0;
		}
		
		nutritionIngested = nut;
	}

	public override IEnumerable<Thing> ButcherProducts( Pawn butcher, float efficiency )
	{
		foreach( var t in InnerPawn.ButcherProducts(butcher, efficiency) )
		{
			yield return t;
		}

		//Spread blood
		if( InnerPawn.RaceProps.BloodDef != null )
            FilthMaker.MakeFilth(butcher.Position, butcher.Map, InnerPawn.RaceProps.BloodDef, InnerPawn.LabelIndefinite() );

		//Thought/tale for butchering humanlike
		if( InnerPawn.RaceProps.Humanlike )
		{
			butcher.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.ButcheredHumanlikeCorpse);
			foreach( var p in butcher.Map.mapPawns.SpawnedPawnsInFaction(butcher.Faction) )
			{
				if( p == butcher || p.needs == null || p.needs.mood == null || p.needs.mood.thoughts == null )
					continue;
				p.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.KnowButcheredHumanlikeCorpse);
			}
			TaleRecorder.RecordTale(TaleDefOf.ButcheredHumanlikeCorpse, butcher);
		}
	}


	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look( ref timeOfDeath, "timeOfDeath" );
        Scribe_Values.Look(ref vanishAfterTimestamp, "vanishAfterTimestamp");
		Scribe_Values.Look(ref everBuriedInSarcophagus, "everBuriedInSarcophagus");
        Scribe_Deep.Look( ref operationsBillStack, "operationsBillStack", this );
        Scribe_Deep.Look( ref innerContainer, "innerContainer", this );
	}

	public void Strip()
    {
        InnerPawn.Strip();
    }

	public override void DrawAt(Vector3 drawLoc, bool flip = false)
	{
		InnerPawn.Drawer.renderer.RenderPawnAt(drawLoc);
	}

	public Thought_Memory GiveObservedThought()
	{
		//Non-humanlike corpses never give thoughts
		if( !InnerPawn.RaceProps.Humanlike )
			return null;

        var storingBuilding = this.StoringThing();
		if( storingBuilding == null )
		{
			//Laying on the ground
            
			Thought_MemoryObservation obs;
			if( this.IsNotFresh() )
				obs = (Thought_MemoryObservation)ThoughtMaker.MakeThought(ThoughtDefOf.ObservedLayingRottingCorpse);
            else
				obs = (Thought_MemoryObservation)ThoughtMaker.MakeThought(ThoughtDefOf.ObservedLayingCorpse);
			obs.Target = this;
			return obs;
		}
        
		return null;
	}

	public override string GetInspectString()
	{
		var sb = new StringBuilder();

		if( InnerPawn.Faction != null )
			sb.AppendLine("Faction".Translate() + ": " + InnerPawn.Faction.Name);

		sb.AppendLine("DeadTime".Translate(Age.ToStringTicksToPeriodVague(vagueMax: false)) );

		float percentMissing = 1f - InnerPawn.health.hediffSet.GetCoverageOfNotMissingNaturalParts(InnerPawn.RaceProps.body.corePart);

		if( percentMissing != 0f )
		{
			sb.AppendLine("CorpsePercentMissing".Translate() + ": " + percentMissing.ToStringPercent());
		}

		sb.AppendLine(base.GetInspectString());
		return sb.ToString().TrimEndNewlines();
	}

	public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
	{
		foreach( var s in base.SpecialDisplayStats() )
		{
			yield return s;
		}

		if( this.GetRotStage() == RotStage.Fresh )
		{
			var meatAmount = StatDefOf.MeatAmount;
			yield return new StatDrawEntry(meatAmount.category, meatAmount, InnerPawn.GetStatValue(meatAmount), StatRequest.For(InnerPawn));

			var leatherAmount = StatDefOf.LeatherAmount;
			yield return new StatDrawEntry(leatherAmount.category, leatherAmount, InnerPawn.GetStatValue(leatherAmount), StatRequest.For(InnerPawn));
		}
	}

	public void RotStageChanged()
	{
		PortraitsCache.SetDirty(InnerPawn);
		NotifyColonistBar();
	}

	private BodyPartRecord GetBestBodyPartToEat(Pawn ingester, float nutritionWanted)
	{
		var candidates = InnerPawn.health.hediffSet.GetNotMissingParts()
			.Where(x => x.depth == BodyPartDepth.Outside && FoodUtility.GetBodyPartNutrition(this, x) > 0.001f);

		if( !candidates.Any() )
			return null;

		// get part which nutrition is the closest to what we want
		return candidates.MinBy(x => Mathf.Abs(FoodUtility.GetBodyPartNutrition(this, x) - nutritionWanted));
	}

	private void NotifyColonistBar()
	{
		if( InnerPawn.Faction == Faction.OfPlayer && Current.ProgramState == ProgramState.Playing )
			Find.ColonistBar.MarkColonistsDirty();
	}
}}
