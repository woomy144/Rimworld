using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Verse;
using Verse.Sound;
using Verse.AI;
using Verse.AI.Group;

namespace RimWorld{
public class Building_Door : Building
{
	//Links
	public CompPowerTrader	powerComp;

	//Working vars - saved
	private bool 			openInt = false;
	private bool			holdOpenInt = false;		//Player has configured door to be/stay open
	private int				lastFriendlyTouchTick = -9999;

	//Working vars - unsaved
	protected int 			ticksUntilClose = 0;
	protected int 			visualTicksOpen = 0;
	private bool			freePassageWhenClearedReachabilityCache;

	//Constants
	private const float		OpenTicks = 45;
	private const int		CloseDelayTicks = 110;
	private const int		WillCloseSoonThreshold = CloseDelayTicks+1;
	private const int		ApproachCloseDelayTicks = 300; //For doors which open before the pawn even arrives, extra long in case pawn is very slow; don't want door to close before they arrive
	private const int		MaxTicksSinceFriendlyTouchToAutoClose = 120;
	private const float		PowerOffDoorOpenSpeedFactor = 0.25f;
	private const float		VisualDoorOffsetStart = 0.0f;
	private const float		VisualDoorOffsetEnd = 0.45f;

	//Properties	
	public bool Open { get { return openInt; } }
	public bool HoldOpen { get { return holdOpenInt; } }
	public bool FreePassage
	{
		get
		{
			//Not open - never free passage
			if( !openInt )
				return false;

			return holdOpenInt || !WillCloseSoon;
		}
	}
	public bool WillCloseSoon
	{
		get
		{
			if( !Spawned )
				return true;

			//It's already closed
			if( !openInt )
				return true;

			//It's held open -> so it won't be closed soon
			if( holdOpenInt )
				return false;

			//Will close soon
			if( ticksUntilClose > 0 && ticksUntilClose <= WillCloseSoonThreshold && !BlockedOpenMomentary )
				return true;

			//Will close automatically soon
			if( CanTryCloseAutomatically && !BlockedOpenMomentary )
				return true;

			//Check if there is any non-hostile non-downed pawn passing through, he will close the door
			for( int i = 0; i < 5; i++ )
			{
				var c = Position + GenAdj.CardinalDirectionsAndInside[i];

				if( !c.InBounds(Map) )
					continue;

				var things = c.GetThingList(Map);
				for( int j = 0; j < things.Count; j++ )
				{
					var p = things[j] as Pawn;

					if( p == null || p.HostileTo(this) || p.Downed )
						continue;

					if( p.Position == Position || (p.pather.Moving && p.pather.nextCell == Position) )
						return true;
				}
			}

			return false;
		}
	}
	public bool BlockedOpenMomentary
	{
		get
		{
			var thingList = Position.GetThingList(Map);
			for( int i=0; i<thingList.Count; i++ )
			{
				var t = thingList[i];
				if( t.def.category == ThingCategory.Item
					|| t.def.category == ThingCategory.Pawn)
					return true;
			}

			return false;
		}
	}
	public bool DoorPowerOn
	{
		get
		{
			return powerComp != null && powerComp.PowerOn;
		}
	}
	public bool SlowsPawns
	{
		get
		{
			return !DoorPowerOn || TicksToOpenNow > 20;
		}
	}
	public int TicksToOpenNow
	{
		get
		{
			float ticks = OpenTicks / this.GetStatValue( StatDefOf.DoorOpenSpeed );

			if( DoorPowerOn )
				ticks *= PowerOffDoorOpenSpeedFactor;

			return Mathf.RoundToInt(ticks);
		}
	}
	private bool CanTryCloseAutomatically
	{
		get
		{
			return FriendlyTouchedRecently && !HoldOpen;
		}
	}
	private bool FriendlyTouchedRecently
	{
		get
		{
			return Find.TickManager.TicksGame < lastFriendlyTouchTick + MaxTicksSinceFriendlyTouchToAutoClose;
		}
	}
	private int VisualTicksToOpen
	{
		get
		{
			return TicksToOpenNow;
		}
	}
	public override bool FireBulwark
	{
		get
		{
			return !Open && base.FireBulwark;
		}
	}

	public override void PostMake()
	{
		base.PostMake();

		powerComp = GetComp<CompPowerTrader>();
	}

	public override void SpawnSetup(Map map, bool respawningAfterLoad)
	{
		base.SpawnSetup(map, respawningAfterLoad);

		powerComp = GetComp<CompPowerTrader>();
		ClearReachabilityCache(map);
		
		// Open the door if we're spawning on top of something
		if( BlockedOpenMomentary )
			DoorOpen();
	}

	public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
	{
		var map = Map;

		base.DeSpawn(mode);

		ClearReachabilityCache(map);
	}

	public override void ExposeData()
	{
		base.ExposeData();

		Scribe_Values.Look(ref openInt, "open", defaultValue: false);
		Scribe_Values.Look(ref holdOpenInt, "holdOpen", defaultValue: false);
		Scribe_Values.Look(ref lastFriendlyTouchTick, "lastFriendlyTouchTick" );

		if( Scribe.mode == LoadSaveMode.LoadingVars )
		{
			if( openInt )
				visualTicksOpen = VisualTicksToOpen;
		}
	}

	public override void SetFaction(Faction newFaction, Pawn recruiter = null)
	{
		base.SetFaction(newFaction, recruiter);

		if( Spawned )
			ClearReachabilityCache(Map);
	}

	public override void Tick()
	{
		base.Tick();

		//Check if we should clear the reachability cache
		if( FreePassage != freePassageWhenClearedReachabilityCache )
			ClearReachabilityCache(Map);

		if( !openInt )
		{
			//Visual - slide door closed
			if( visualTicksOpen > 0 )
				visualTicksOpen--;

			//Equalize temperatures
			if( (Find.TickManager.TicksGame + thingIDNumber.HashOffset()) % TemperatureTuning.Door_TempEqualizeIntervalClosed == 0 )
				GenTemperature.EqualizeTemperaturesThroughBuilding(this, TemperatureTuning.Door_TempEqualizeRate, twoWay: false);
		}
		else if( openInt )
		{
			//Visual - slide door open
			if( visualTicksOpen < VisualTicksToOpen )
				visualTicksOpen++;

			//Check friendly touched
			var things = Position.GetThingList(Map);
			for( int i = 0; i < things.Count; i++ )
			{
				var p = things[i] as Pawn;
				if( p != null )
					CheckFriendlyTouched(p);
			}

			//Count down to closing
			if( ticksUntilClose > 0 )
			{
				//Pawn moving
				if( Map.thingGrid.CellContains( Position, ThingCategory.Pawn ) )
				{
					//This is important for doors which !SlowsPawns, this will override their default long approach close delay when the pawn actually enters the cell,
					//note that we do this only if ticksUntilClose is already > 0
					ticksUntilClose = CloseDelayTicks;
				}
					
				ticksUntilClose--;
				if( ticksUntilClose <= 0 && !holdOpenInt )
				{
					if( !DoorTryClose() )
						ticksUntilClose = 1; //Something blocking - try next tick
				}
			}
			else
			{
				//Not assigned to close, check if we want to close automatically
				if( CanTryCloseAutomatically )
					ticksUntilClose = CloseDelayTicks;
			}

			//Equalize temperatures
			if( (Find.TickManager.TicksGame + thingIDNumber.HashOffset()) % TemperatureTuning.Door_TempEqualizeIntervalOpen == 0 )
				GenTemperature.EqualizeTemperaturesThroughBuilding(this, TemperatureTuning.Door_TempEqualizeRate, twoWay: false);
		}
	}

	public void CheckFriendlyTouched(Pawn p)
	{
		if( !p.HostileTo(this) && PawnCanOpen(p) )
			lastFriendlyTouchTick = Find.TickManager.TicksGame;
	}

	

	public void Notify_PawnApproaching( Pawn p, int moveCost )
	{
		CheckFriendlyTouched(p);

		if( PawnCanOpen(p) )
		{
			Map.fogGrid.Notify_PawnEnteringDoor(this, p);

			//Open automatically before pawn arrives
			if( !SlowsPawns )
			{
				//Make sure it stays open before the pawn reaches it
				int delay = Mathf.Max(ApproachCloseDelayTicks, moveCost + 1);
				DoorOpen(delay);
			}
		}
	}

	/// <summary>
	/// Returns whether p can physically pass through the door without bashing.
	/// </summary>
	public bool CanPhysicallyPass( Pawn p )
	{
		return FreePassage
			|| PawnCanOpen(p)
			|| (Open && p.HostileTo(this)); // hostile pawns can always pass if the door is open
	}

	/// <summary>
	/// Returns whether p can open the door without bashing.
	/// </summary>
	public virtual bool PawnCanOpen( Pawn p )
	{
		var lord = p.GetLord();
		if( lord != null && lord.LordJob != null && lord.LordJob.CanOpenAnyDoor(p) )
			return true;

		//This is to avoid situations where a wild man is stuck inside the colony forever right after having a mental break
		if( WildManUtility.WildManShouldReachOutsideNow(p) )
			return true;

		//Door has no faction?
		if( Faction == null )
			return true;

		if( p.guest != null && p.guest.Released )
			return true;

		return GenAI.MachinesLike(Faction, p);
	}
	
	public override bool BlocksPawn( Pawn p )
	{
		if( openInt )
			return false;
		else
			return !PawnCanOpen(p);
	}

	protected void DoorOpen(int ticksToClose = CloseDelayTicks)
	{
		if( openInt )
			ticksUntilClose = ticksToClose;
		else //We need to add TicksToOpenNow because this is how long the pawn will wait before the door opens (by using a busy stance)
			ticksUntilClose = TicksToOpenNow + ticksToClose;

		if( !openInt )
		{
			openInt = true;

			CheckClearReachabilityCacheBecauseOpenedOrClosed();

			if( DoorPowerOn )
				def.building.soundDoorOpenPowered.PlayOneShot(new TargetInfo(Position, Map));
			else
				def.building.soundDoorOpenManual.PlayOneShot(new TargetInfo(Position, Map));
		}
	}


	protected bool DoorTryClose()
	{
		if( holdOpenInt || BlockedOpenMomentary )
			return false;

		openInt = false;
		
		CheckClearReachabilityCacheBecauseOpenedOrClosed();

		if( DoorPowerOn )
			def.building.soundDoorClosePowered.PlayOneShot(new TargetInfo(Position, Map));
		else
			def.building.soundDoorCloseManual.PlayOneShot(new TargetInfo(Position, Map));

		return true;
	}

		
	public void StartManualOpenBy( Pawn opener )
	{
		DoorOpen();
	}

	public void StartManualCloseBy( Pawn closer )
	{
		ticksUntilClose = CloseDelayTicks;
	}

	public override void Draw()
	{
		//Note: It's a bit odd that I'm changing game variables in Draw
		//      but this is the easiest way to make this always look right even if
		//      conditions change while the game is paused.
		Rotation = DoorRotationAt(Position, Map);

		//Draw the two moving doors
		float pctOpen = Mathf.Clamp01((float)visualTicksOpen / (float)VisualTicksToOpen);	//Needs clamp for after game load		
		float offsetDist = VisualDoorOffsetStart + (VisualDoorOffsetEnd-VisualDoorOffsetStart)*pctOpen;	

		for( int i=0; i<2; i++ )
		{
			//Left, then right
			Vector3 offsetNormal = new Vector3();
			Mesh mesh;
			if( i == 0 )
			{
				offsetNormal = new Vector3(0,0,-1);
				mesh = MeshPool.plane10;
			}
			else
			{
				offsetNormal = new Vector3(0,0,1);
				mesh = MeshPool.plane10Flip;
			}
			

			//Work out move direction
			Rot4 openDir = Rotation;
			openDir.Rotate(RotationDirection.Clockwise);
			offsetNormal  = openDir.AsQuat * offsetNormal;

			//Position the door
			Vector3 doorPos =  DrawPos;
			doorPos.y = Altitudes.AltitudeFor(AltitudeLayer.DoorMoveable);
			doorPos += offsetNormal * offsetDist;
		
			//Draw!
			Graphics.DrawMesh(mesh, doorPos, Rotation.AsQuat, Graphic.MatAt(Rotation), 0 );
		}
			
		Comps_PostDraw();
	}


	private static int AlignQualityAgainst( IntVec3 c, Map map )
	{
		if( !c.InBounds(map) )
			return 0;

		//We align against anything unwalkthroughable and against blueprints for unwalkthroughable things
		if( !c.Walkable(map) )
			return 9;
			
		List<Thing> things = c.GetThingList(map);
		for(int i=0; i<things.Count; i++ )
		{
			Thing t = things[i];

			if( typeof(Building_Door).IsAssignableFrom(t.def.thingClass) )
				return 1;

			Thing blue = t as Blueprint;
			if( blue != null )
			{
				if( blue.def.entityDefToBuild.passability == Traversability.Impassable )
					return 9;
				if( typeof(Building_Door).IsAssignableFrom(t.def.thingClass) )
					return 1;
			}
		}
			
		return 0;		
	}

	public static Rot4 DoorRotationAt(IntVec3 loc, Map map)
	{
		int horVotes = 0;
		int verVotes = 0;

		horVotes += AlignQualityAgainst( loc + IntVec3.East, map );
		horVotes += AlignQualityAgainst( loc + IntVec3.West, map );
		verVotes += AlignQualityAgainst( loc + IntVec3.North, map );
		verVotes += AlignQualityAgainst( loc + IntVec3.South, map );

		if( horVotes >= verVotes )
			return Rot4.North;
		else
			return Rot4.East;
	}

	public override IEnumerable<Gizmo> GetGizmos()
	{
		foreach( var g in base.GetGizmos() )
		{
			yield return g;
		}

		if( Faction == Faction.OfPlayer )
		{
			var ro = new Command_Toggle();
			ro.defaultLabel = "CommandToggleDoorHoldOpen".Translate();
			ro.defaultDesc = "CommandToggleDoorHoldOpenDesc".Translate();
			ro.hotKey = KeyBindingDefOf.Misc3;
			ro.icon = TexCommand.HoldOpen;
			ro.isActive = () => holdOpenInt;
			ro.toggleAction = () => holdOpenInt = !holdOpenInt;
			yield return ro;
		}
	}

	private void ClearReachabilityCache(Map map)
	{
		map.reachability.ClearCache();
		freePassageWhenClearedReachabilityCache = FreePassage;
	}

	private void CheckClearReachabilityCacheBecauseOpenedOrClosed()
	{
		if( Spawned )
			Map.reachability.ClearCacheForHostile(this);
	}
}

public static class DoorsDebugDrawer
{
	public static void DrawDebug()
	{
		if( !DebugViewSettings.drawDoorsDebug )
			return;

		var visibleRect = Find.CameraDriver.CurrentViewRect;
		var buildings = Find.CurrentMap.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);

		for( int i = 0; i < buildings.Count; i++ )
		{
			if( !visibleRect.Contains(buildings[i].Position) )
				continue;

			var door = buildings[i] as Building_Door;

			if( door != null )
			{
				Color color;

				if( door.FreePassage )
					color = new Color(0f, 1f, 0f, 0.5f);
				else
					color = new Color(1f, 0f, 0f, 0.5f);

				CellRenderer.RenderCell(door.Position, SolidColorMaterials.SimpleSolidColorMaterial(color));
			}
		}
	}
}

}


