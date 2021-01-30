using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;



namespace RimWorld{
public class CompGatherSpot : ThingComp
{
	//Working vars
	private bool active = true;
	
	public bool Active
	{
		get
		{
			return active;
		}
		set
		{
			if( value == active )
				return;

			active = value;

			if( parent.Spawned )
			{
				if( active )
					parent.Map.gatherSpotLister.RegisterActivated(this);
				else
					parent.Map.gatherSpotLister.RegisterDeactivated(this);
			}
		}
	}


	public override void PostExposeData()
	{
		Scribe_Values.Look(ref active, "active", false);	
	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);

		if( Active )
			parent.Map.gatherSpotLister.RegisterActivated(this);
	}

	public override void PostDeSpawn(Map map)
	{
		base.PostDeSpawn(map);

		if( Active )
			map.gatherSpotLister.RegisterDeactivated(this);
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		Command_Toggle com = new Command_Toggle();
		com.hotKey = KeyBindingDefOf.Command_TogglePower;
		com.defaultLabel = "CommandGatherSpotToggleLabel".Translate();
		com.icon = TexCommand.GatherSpotActive;
		com.isActive = ()=>Active;
		com.toggleAction = ()=>Active = !Active;

		if( Active )
			com.defaultDesc = "CommandGatherSpotToggleDescActive".Translate();
		else
			com.defaultDesc = "CommandGatherSpotToggleDescInactive".Translate();
		
		yield return com;
	}
}



public class GatherSpotLister
{
	public List<CompGatherSpot> activeSpots = new List<CompGatherSpot>();

	public void RegisterActivated( CompGatherSpot spot )
	{
		if( !activeSpots.Contains(spot) )
			activeSpots.Add(spot);
	}

	public void RegisterDeactivated( CompGatherSpot spot )
	{
		if( activeSpots.Contains(spot) )
			activeSpots.Remove(spot);
	}
}}