using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse.Sound;
using UnityEngine;
using RimWorld;



namespace Verse{
public class Building : ThingWithComps
{
	//Working vars
	private Sustainer		sustainerAmbient = null;

	//Config
	public bool				canChangeTerrainOnDestroyed = true;

	//Properties
	public CompPower PowerComp		{get{ return GetComp<CompPower>(); }}
	public virtual bool TransmitsPowerNow
	{
		get
		{
			//Designed to be overridden
			//In base game this always just returns the value in the powercomp's def
			CompPower pc = PowerComp;
			return pc != null && pc.Props.transmitsPower;
		}
	}
	public override int HitPoints
	{
		set
		{
			int oldHitPoints = HitPoints;
			base.HitPoints = value;

			BuildingsDamageSectionLayerUtility.Notify_BuildingHitPointsChanged(this, oldHitPoints);
		}
	}

	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref canChangeTerrainOnDestroyed, "canChangeTerrainOnDestroyed", true);
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		//Before base.SpawnSetup() so when regions are rebuilt this building can be accessed via edificeGrid
		if( def.IsEdifice() )
			map.edificeGrid.Register(this);

		base.SpawnSetup(map, respawningAfterLoad);

		Map.listerBuildings.Add(this);
		
		//Remake terrain meshes with new underwall under me
		if( def.coversFloor )
			Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.Terrain, true, false);

		var occRect = this.OccupiedRect();
		for( int z=occRect.minZ; z<=occRect.maxZ; z++ )
		{
			for( int x=occRect.minX; x<=occRect.maxX; x++ )
			{
				var c = new IntVec3(x,0,z);
				Map.mapDrawer.MapMeshDirty( c, MapMeshFlag.Buildings );
				Map.glowGrid.MarkGlowGridDirty(c);
				if( !SnowGrid.CanCoexistWithSnow(def) )
					Map.snowGrid.SetDepth(c, 0);
			}
		}

		if( Faction == Faction.OfPlayer )
		{
			if( def.building != null && def.building.spawnedConceptLearnOpportunity != null )
			{
				LessonAutoActivator.TeachOpportunity( def.building.spawnedConceptLearnOpportunity, OpportunityType.GoodToKnow );
			}
		}

		AutoHomeAreaMaker.Notify_BuildingSpawned( this );

		if( def.building != null && !def.building.soundAmbient.NullOrUndefined() )
		{
			LongEventHandler.ExecuteWhenFinished(() =>
				{
					SoundInfo info = SoundInfo.InMap(this, MaintenanceType.None);
					sustainerAmbient = SoundStarter.TrySpawnSustainer(def.building.soundAmbient, info);
				});
		}

		Map.listerBuildingsRepairable.Notify_BuildingSpawned(this);

		if( !this.CanBeSeenOver() )
			Map.exitMapGrid.Notify_LOSBlockerSpawned();

        SmoothSurfaceDesignatorUtility.Notify_BuildingSpawned(this);
		
		//Must go after adding to buildings list
		map.avoidGrid.Notify_BuildingSpawned(this);
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		var map = Map; // before DeSpawn!

		base.DeSpawn(mode);

		if( def.IsEdifice() )
			map.edificeGrid.DeRegister(this);

		if( mode != DestroyMode.WillReplace )
		{
			if( def.MakeFog )
				map.fogGrid.Notify_FogBlockerRemoved(Position);

			if( def.holdsRoof )
				RoofCollapseCellsFinder.Notify_RoofHolderDespawned(this, map);
		
			if( def.IsSmoothable )
				SmoothSurfaceDesignatorUtility.Notify_BuildingDespawned(this, map);
		}

		if( sustainerAmbient != null )
			sustainerAmbient.End();

		CellRect occRect = GenAdj.OccupiedRect(this);
		for( int z=occRect.minZ; z<=occRect.maxZ; z++ )
		{
			for( int x=occRect.minX; x<=occRect.maxX; x++ )
			{
				IntVec3 c = new IntVec3(x,0,z);

				MapMeshFlag changeType = MapMeshFlag.Buildings;

				if( def.coversFloor )
					changeType |= MapMeshFlag.Terrain;

				if( def.Fillage == FillCategory.Full )
				{
					changeType |= MapMeshFlag.Roofs;
					changeType |= MapMeshFlag.Snow;
				}

				map.mapDrawer.MapMeshDirty( c, changeType );

				map.glowGrid.MarkGlowGridDirty(c);
			}
		}

		map.listerBuildings.Remove(this);
		map.listerBuildingsRepairable.Notify_BuildingDeSpawned(this);

		if( def.building.leaveTerrain != null && Current.ProgramState == ProgramState.Playing && canChangeTerrainOnDestroyed )
		{
			for( var cri = GenAdj.OccupiedRect(this).GetIterator(); !cri.Done(); cri.MoveNext() )
			{
				map.terrainGrid.SetTerrain(cri.Current, def.building.leaveTerrain);
			}
		}

		//Mining, planning, etc
		map.designationManager.Notify_BuildingDespawned(this);

		if( !this.CanBeSeenOver() )
			map.exitMapGrid.Notify_LOSBlockerDespawned();

		if( def.building.hasFuelingPort )
		{
			var fuelingPortCell = FuelingPortUtility.GetFuelingPortCell(Position, Rotation);
			var launchable = FuelingPortUtility.LaunchableAt(fuelingPortCell, map);

			if( launchable != null )
				launchable.Notify_FuelingPortSourceDeSpawned();
		}

		//Must go after removing from buildings list
		map.avoidGrid.Notify_BuildingDespawned(this);
	}
	
	public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
	{
		bool wasSpawned = Spawned;
		var map = Map; // before Destroy()!

		// before Destroy(); the math is easier to do here
		SmoothableWallUtility.Notify_BuildingDestroying(this, mode);

		base.Destroy(mode);

		// (buildings can be reinstalled)
		InstallBlueprintUtility.CancelBlueprintsFor(this);

		if( mode == DestroyMode.Deconstruct && wasSpawned )
			SoundDefOf.Building_Deconstructed.PlayOneShot(new TargetInfo(Position, map));
		
		if( wasSpawned )
			ThingUtility.CheckAutoRebuildOnDestroyed(this, mode, map, def);
	}


	public override void Draw()
	{
		//If we've already added to the map mesh don't bother with drawing our base mesh
		if( def.drawerType == DrawerType.RealtimeOnly )
			base.Draw();
		else
			Comps_PostDraw();
	}

	public override void SetFaction( Faction newFaction, Pawn recruiter = null )
	{
		if( Spawned )
		{
			Map.listerBuildingsRepairable.Notify_BuildingDeSpawned(this);
			Map.listerBuildings.Remove(this);
		}

		base.SetFaction(newFaction, recruiter);

		if( Spawned )
		{
			Map.listerBuildingsRepairable.Notify_BuildingSpawned(this);
			Map.listerBuildings.Add(this);
			Map.mapDrawer.MapMeshDirty(Position, MapMeshFlag.PowerGrid, true, false);

			if( newFaction == Faction.OfPlayer )
				AutoHomeAreaMaker.Notify_BuildingClaimed(this);
		}
	}

	public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
	{
		if( Faction != null && Spawned && Faction != Faction.OfPlayer )
		{
			for( int i=0; i<Map.lordManager.lords.Count; i++ )
			{
				var lord = Map.lordManager.lords[i];
				if( lord.faction == Faction )
					lord.Notify_BuildingDamaged(this, dinfo);
			}
		}

		base.PreApplyDamage(ref dinfo, out absorbed);
	}

	public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
	{
		base.PostApplyDamage(dinfo, totalDamageDealt);

		if( Spawned )
			Map.listerBuildingsRepairable.Notify_BuildingTookDamage(this);
	}

	public override void DrawExtraSelectionOverlays()
	{
		base.DrawExtraSelectionOverlays();

		var ebp = InstallBlueprintUtility.ExistingBlueprintFor(this);

		if( ebp != null )
			GenDraw.DrawLineBetween(this.TrueCenter(), ebp.TrueCenter());
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach( var c in base.GetGizmos() )
		{
			yield return c;
		}

		if( def.Minifiable && Faction == Faction.OfPlayer )
			yield return InstallationDesignatorDatabase.DesignatorFor(def);

		var buildCopy = BuildCopyCommandUtility.BuildCopyCommand(def, Stuff);
		if( buildCopy != null )
			yield return buildCopy;
			
		if( Faction == Faction.OfPlayer )
		{
			foreach( var facility in BuildFacilityCommandUtility.BuildFacilityCommands(def) )
			{
				yield return facility;
			}
		}
	}

	public virtual bool ClaimableBy(Faction by)
	{
		if( !def.Claimable )
			return false;

		if( Faction != null )
		{
			if( Faction == by )
				return false;

			if( by == Faction.OfPlayer )
			{
				// if there's any undowned humanlike of this faction spawned here, then don't allow claiming this building

				if( Faction == Faction.OfInsects )
				{
					if( HiveUtility.AnyHivePreventsClaiming(this) )
						return false;
				}
				else if( Spawned )
				{
					var pawns = Map.mapPawns.SpawnedPawnsInFaction(Faction);

					for( int i = 0; i < pawns.Count; i++ )
					{
						if( pawns[i].RaceProps.Humanlike && GenHostility.IsActiveThreatToPlayer(pawns[i]) )
							return false;
					}
				}
			}
		}

		return true;
	}

	public virtual bool DeconstructibleBy(Faction faction)
	{
		if( DebugSettings.godMode )
			return true;

		if( !def.building.IsDeconstructible )
			return false;

		return Faction == faction
			|| ClaimableBy(faction)
			|| def.building.alwaysDeconstructible;
	}

	public virtual ushort PathWalkCostFor(Pawn p)
	{
		return 0;
	}

	public virtual bool IsDangerousFor(Pawn p)
	{
		return false;
	}
}
}
