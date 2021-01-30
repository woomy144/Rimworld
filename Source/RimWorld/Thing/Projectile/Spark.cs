using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Verse;


namespace RimWorld{
public class Spark : Projectile
{
	protected override void Impact(Thing hitThing)
	{
		var map = Map; // before Impact!

		base.Impact(hitThing);

		FireUtility.TryStartFireIn(Position, map, Fire.MinFireSize);
	}	
}}
