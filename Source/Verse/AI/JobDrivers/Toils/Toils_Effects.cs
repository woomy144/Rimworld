using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.Sound;

namespace Verse.AI{
public static class Toils_Effects
{
	public static Toil MakeSound( SoundDef soundDef )
	{
		Toil toil = new Toil();
		toil.initAction = ()=>
		{
			Pawn actor = toil.actor;
			soundDef.PlayOneShot(new TargetInfo(actor.Position, actor.Map));	
		};
		return toil;
	}
}}