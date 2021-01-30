using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI.Group;


namespace RimWorld{
public class CompForbiddable : ThingComp
{
	//Working vars
	private bool forbiddenInt = false;

	//Properties
	public bool Forbidden
	{
		get
		{
			return forbiddenInt;
		}
		set
		{
			if( value == forbiddenInt )
				return;
			
			forbiddenInt = value;

			if( parent.Spawned )
			{
				if( forbiddenInt )
				{
					parent.Map.listerHaulables.Notify_Forbidden(parent);
					parent.Map.listerMergeables.Notify_Forbidden(parent);
				}
				else
				{
					parent.Map.listerHaulables.Notify_Unforbidden(parent);
					parent.Map.listerMergeables.Notify_Unforbidden(parent);
				}

				//Forbidden doors affect reachability!
				//(but forbidden cells don't, look at IsForbiddenToPass and Region.Allows() (used by Reachability) for more information)
				if( parent is Building_Door )
					parent.Map.reachability.ClearCache();
			}
		}
	}

	public override void PostExposeData()
	{
		Scribe_Values.Look(ref forbiddenInt, "forbidden", false);	
	}
	
	public override void PostDraw()
	{	
		if( forbiddenInt )
		{
			if( parent is Blueprint || parent is Frame )
			{
				// for blueprints and frames we draw big overlay only if size is greater than 1x1
				if( parent.def.size.x > 1 || parent.def.size.z > 1 )
					parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.ForbiddenBig);
				else
					parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.Forbidden);
			}
			else if( parent.def.category == ThingCategory.Building )
				parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.ForbiddenBig);
			else
				parent.Map.overlayDrawer.DrawOverlay(parent, OverlayTypes.Forbidden);
		}
	}
	
	public override void PostSplitOff( Thing piece )
	{
		piece.SetForbidden( forbiddenInt );
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		//You can only toggle forbidden on your own buildings
		//It makes no sense to do it on enemies' buildings, which you can't use anyway
		if( parent is Building && parent.Faction != Faction.OfPlayer )
			yield break;

		var com = new Command_Toggle();
		com.hotKey = KeyBindingDefOf.Command_ItemForbid;
		com.icon = TexCommand.ForbidOff;
		com.isActive = ()=>!Forbidden;
		com.defaultLabel = "CommandAllow".TranslateWithBackup("DesignatorUnforbid");

		if( forbiddenInt )
			com.defaultDesc = "CommandForbiddenDesc".TranslateWithBackup("DesignatorUnforbidDesc");
		else
			com.defaultDesc = "CommandNotForbiddenDesc".TranslateWithBackup("DesignatorForbidDesc");

		if( parent.def.IsDoor )
		{
			com.tutorTag = "ToggleForbidden-Door";
			com.toggleAction = ()=>
			{
				Forbidden = !Forbidden;
				PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.ForbiddingDoors, KnowledgeAmount.SpecificInteraction);
			};
		}
		else
		{
			com.tutorTag = "ToggleForbidden";
			com.toggleAction = ()=>
			{
				Forbidden = !Forbidden;
				PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Forbidding, KnowledgeAmount.SpecificInteraction);
			};
		}

		yield return com;
	}
}


public static class ForbidUtility
{
	public static void SetForbidden(this Thing t, bool value, bool warnOnFail = true)
	{
		if( t == null )
		{
			if( warnOnFail )
				Log.Error("Tried to SetForbidden on null Thing." );
			return;
		}

		ThingWithComps twc = t as ThingWithComps;
		if( twc == null )
		{
			if( warnOnFail )
				Log.Error("Tried to SetForbidden on non-ThingWithComps Thing " + t );
			return;
		}
		
		CompForbiddable f = twc.GetComp<CompForbiddable>();
		if( f == null )
		{
			if( warnOnFail )
				Log.Error("Tried to SetForbidden on non-Forbiddable Thing " + t );
			
			return;
		}
		
		f.Forbidden = value;
	}

	public static void SetForbiddenIfOutsideHomeArea(this Thing t)
	{
		if( !t.Spawned )
			Log.Error("SetForbiddenIfOutsideHomeArea unspawned thing " + t);

		if( t.Position.InBounds(t.Map) && !t.Map.areaManager.Home[t.Position] )
			SetForbidden(t, true, false);
	}

	//===========================================================================

	public static bool CaresAboutForbidden(Pawn pawn, bool cellTarget)
	{
		if( pawn.HostFaction != null )
		{
			//Player guests care about forbidden if not in a player-home-map,
			//this is so hungry prisoners don't try to eat forbidden meals in hostile
			//faction bases and die
			bool securePlayerGuestInNonPlayerHome = pawn.HostFaction == Faction.OfPlayer
				&& pawn.Spawned
				&& !pawn.Map.IsPlayerHome
				&& (pawn.GetRoom() == null || !pawn.GetRoom().isPrisonCell) // not in a prison cell
				&& (!pawn.IsPrisoner || pawn.guest.PrisonerIsSecure); // not escaping

			if( !securePlayerGuestInNonPlayerHome )
				return false;
		}

		if( pawn.InMentalState )
			return false;
		
		//Animals following drafted masters don't care about forbidden cells (only things, like doors)
		if( cellTarget && ThinkNode_ConditionalShouldFollowMaster.ShouldFollowMaster(pawn) )
			return false;

		return true;
	}

	/// <summary>
	/// This assumes that this cell's map is the same as the pawn's MapHeld.
	/// </summary>
	public static bool InAllowedArea(this IntVec3 c, Pawn forPawn)
	{
		if( forPawn.playerSettings != null )
		{
			var aa = forPawn.playerSettings.EffectiveAreaRestrictionInPawnCurrentMap;
			if( aa != null && aa.TrueCount > 0 && !aa[c] ) // (we treat allowed areas with 0 true count as if they never existed)
				return false;
		}

		return true;
	}

	/// <summary>
	/// Returns true if t is forbidden as a destination for pawn in the pawn's current state.
	/// </summary>
	public static bool IsForbidden(this Thing t, Pawn pawn)
	{
		if( !CaresAboutForbidden(pawn, false) )
			return false;

		//Is t's position forbidden?
		if( t.Spawned && t.Position.IsForbidden(pawn) )
			return true;

		//Is t forbidden directly?
		if( IsForbidden(t, pawn.Faction) || IsForbidden(t, pawn.HostFaction) )
			return true;

		//Is it one of lord's extra forbidden things?
		var lord = pawn.GetLord();
		if( lord != null && lord.extraForbiddenThings.Contains(t) )
			return true;

		return false;
	}

	/// <summary>
	/// Returns true if t is forbidden for pawn to pass in the pawn's current state.
	/// Note: We pass doors even if their position is forbidden (because forbidding cells doesn't affect reachability - it's only a hint).
	/// </summary>
	public static bool IsForbiddenToPass(this Building_Door t, Pawn pawn)
	{
		if( !CaresAboutForbidden(pawn, false) )
			return false;

		//We don't want to intepret doors as forbidden just because their cell is
		//Allowed areas reflect allowed destinations and preferred paths - not allowed paths
		//However, doors can be destinations if they're being repaired or something (which is handled by IsForbidden()).

		//Is thing forbidden directly?
		if( IsForbidden(t, pawn.Faction) )
			return true;

		return false;
	}

	/// <summary>
	/// Returns true if the cell is forbidden for pawn in the pawn's current state.
	/// This assumes that the cell's map is the same as the pawn's map.
	/// </summary>
	public static bool IsForbidden(this IntVec3 c, Pawn pawn)
	{
		if( !CaresAboutForbidden(pawn, true) )
			return false;

		//Forbidden because outside allowed area?
		if( !c.InAllowedArea(pawn) )
			return true;
		
		//Forbidden because outside squad-flag restriction zone?
		if( pawn.mindState.maxDistToSquadFlag > 0 && !c.InHorDistOf(pawn.DutyLocation(), pawn.mindState.maxDistToSquadFlag) )
			return true;

		return false;
	}

	/// <summary>
	/// Returns true if the region is entirely outside pawn's allowed area.
	/// </summary>
	public static bool IsForbiddenEntirely(this Region r, Pawn pawn)
	{
		if( !CaresAboutForbidden(pawn, true) )
			return false;

		//Forbidden because outside allowed area?
		if( pawn.playerSettings != null )
		{
			var aa = pawn.playerSettings.EffectiveAreaRestriction;
			if( aa != null && aa.TrueCount > 0 && aa.Map == r.Map && r.OverlapWith(aa) == AreaOverlap.None ) // (we treat allowed areas with 0 true count as if they never existed)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Returns true if the thing is marked forbidden for the faction (e.g. by direct per-thing forbid marking).
	/// If you're checking in relation to a pawn, use IsForbidden with the pawn directly.
	/// </summary>
	public static bool IsForbidden(this Thing t, Faction faction)
	{
		if( faction == null )
			return false;

		if( faction != Faction.OfPlayer )
			return false;

		var twc = t as ThingWithComps;
		if( twc == null )
			return false;
		
		var f = twc.GetComp<CompForbiddable>();
		if( f == null )
			return false;
		
		return f.Forbidden;
	}

}}







