using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorld
{

//Note: "Storage" here means a cell-based storage (e.g. a shelf), it's not a container like graves where the item disappears.
//Maybe we should rename it to Building_CellStorage and add a Building_Storage as a base class for all haul destinations like shelves and graves?
public class Building_Storage : Building, ISlotGroupParent
{
	//Working vars
	public StorageSettings	settings;
	
	//Working vars - unsaved
	public SlotGroup		slotGroup;
	private List<IntVec3>	cachedOccupiedCells = null;

	public Building_Storage()
	{
		slotGroup = new SlotGroup(this);
	}

	//=======================================================================
	//========================== SlotGroupParent interface===================
	//=======================================================================

	public bool StorageTabVisible { get { return true; } }
    public bool IgnoreStoredThingsBeauty { get { return def.building.ignoreStoredThingsBeauty; } }
	public SlotGroup GetSlotGroup(){return slotGroup;}
	public virtual void Notify_ReceivedThing(Thing newItem)
	{
		if( Faction == Faction.OfPlayer && newItem.def.storedConceptLearnOpportunity != null )
			LessonAutoActivator.TeachOpportunity(newItem.def.storedConceptLearnOpportunity, OpportunityType.GoodToKnow);
	}
	public virtual void Notify_LostThing(Thing newItem){/*Nothing by default*/}
	public virtual IEnumerable<IntVec3> AllSlotCells()
	{
		foreach( IntVec3 c in GenAdj.CellsOccupiedBy(this) )
		{
			yield return c;
		}
	}
	public List<IntVec3> AllSlotCellsList()
	{
		if( cachedOccupiedCells == null )
			cachedOccupiedCells = AllSlotCells().ToList();

		return cachedOccupiedCells;
	}
	public StorageSettings GetStoreSettings()
	{
		return settings;
	}
	public StorageSettings GetParentStoreSettings()
	{
		return def.building.fixedStorageSettings;
	}
	public string SlotYielderLabel(){return LabelCap;}
	public bool Accepts(Thing t)
	{
		return settings.AllowedToAccept(t);
	}


	//=======================================================================
	//============================== Other stuff ============================
	//=======================================================================

	public override void PostMake()
	{
		base.PostMake();

		settings = new StorageSettings(this);

		if( def.building.defaultStorageSettings != null )
			settings.CopyFrom( def.building.defaultStorageSettings );
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		cachedOccupiedCells = null; // invalidate cache

		base.SpawnSetup(map, respawningAfterLoad);
	}
	
	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Deep.Look(ref settings, "settings", this);
	}
	
	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach( var g in base.GetGizmos() )
		{
			yield return g;
		}

		foreach( var g in StorageSettingsClipboard.CopyPasteGizmosFor(settings) )
		{
			yield return g;
		}
	}
}

}