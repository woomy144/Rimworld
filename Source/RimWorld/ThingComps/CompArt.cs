using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.Grammar;

namespace RimWorld
{
public enum ArtGenerationContext : byte
{
	Outsider,
	Colony,
}

public class CompArt : ThingComp
{
	//Data
	private string			authorNameInt = null;
	private string			titleInt = null;
	private TaleReference	taleRef = null;

	//Properties
	public string AuthorName
	{
		get
		{
			if( authorNameInt.NullOrEmpty() )
				return "UnknownLower".Translate().CapitalizeFirst();
			
			return authorNameInt;
		}
	}
	public string Title
	{
		get
		{
			if( titleInt.NullOrEmpty() )
			{
				Log.Error("CompArt got title but it wasn't configured.");
				titleInt = "Error";
			}

			return titleInt;
		}
	}
	public TaleReference TaleRef { get { return taleRef; } }
	public bool CanShowArt
	{
		get
		{
			//Graves must be full
			if( Props.mustBeFullGrave )
			{
				var grave = parent as Building_Grave;
				if( grave == null || !grave.HasCorpse )
					return false;
			}

			//Must either have no quality, or sufficient quality to show art
			QualityCategory qc;
			if( !parent.TryGetQuality(out qc) )
				return true;
			return qc >= Props.minQualityForArtistic;
		}
	}
	public bool Active
	{
		get
		{
			return taleRef != null;
		}
	}
	public CompProperties_Art Props { get { return (CompProperties_Art)props; } }



	public void InitializeArt( ArtGenerationContext source )
	{
		InitializeArt( null, source );
	}

	public void InitializeArt( Thing relatedThing )
	{
		InitializeArt( relatedThing, ArtGenerationContext.Colony );
	}

	private void InitializeArt( Thing relatedThing, ArtGenerationContext source )
	{
		if( taleRef != null )
		{
			//Is there any scenario where this should happen more than once?
			//Yes, theoretically. For example, we bury a corpse in a sarcophagus, then dig him up and bury another.
			//The art changes in response. But is this correct?
			taleRef.ReferenceDestroyed();
			taleRef = null;
		}

		if( CanShowArt )
		{
			if( Current.ProgramState == ProgramState.Playing )
			{
				if( relatedThing != null )
					taleRef = Find.TaleManager.GetRandomTaleReferenceForArtConcerning(relatedThing);
				else
					taleRef = Find.TaleManager.GetRandomTaleReferenceForArt(source);
			}
			else
				taleRef = TaleReference.Taleless;  //Todo add some chance of getting taleless art even in map play

			titleInt = GenerateTitle();
		}
		else
		{
			titleInt = null;
			taleRef = null;
		}
	}

	public void JustCreatedBy( Pawn pawn )
	{
		if( CanShowArt )
			authorNameInt = pawn.Name.ToStringFull;
	}

	public void Clear()
	{
		authorNameInt = null;
		titleInt = null;

		if( taleRef != null )
		{
			taleRef.ReferenceDestroyed();
			taleRef = null;
		}
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref authorNameInt, "authorName", null);
		Scribe_Values.Look(ref titleInt, "title", null);
		Scribe_Deep.Look(ref taleRef, "taleRef");
	}

	public override string CompInspectStringExtra()
	{
		if( !Active )
			return null;

		string str = "Author".Translate() + ": " + AuthorName;
		str += "\n" + "Title".Translate() + ": " + Title;
		return str;
	}

	public override void PostDestroy(DestroyMode mode, Map previousMap)
	{
		base.PostDestroy(mode, previousMap);

		if( taleRef != null )
		{
			taleRef.ReferenceDestroyed();
			taleRef = null;
		}
	}

	public override string GetDescriptionPart()
	{
		if( !Active )
			return null;

		string str = "";
		str += Title;
		str += "\n\n";
		str += GenerateImageDescription();
		str += "\n\n";
		str += "Author".Translate() + ": " + AuthorName;
		return str;
	}

	public override bool AllowStackWith(Thing other)
	{
		if( Active )
			return false;

		return true;
	}

	public string GenerateImageDescription()
	{
		if( taleRef == null )
		{
			Log.Error("Did CompArt.GenerateImageDescription without initializing art: " + parent);
			InitializeArt(ArtGenerationContext.Outsider);
		}

		return taleRef.GenerateText(TextGenerationPurpose.ArtDescription, Props.descriptionMaker);
	}

	private string GenerateTitle()
	{
		if( taleRef == null )
		{
			Log.Error("Did CompArt.GenerateTitle without initializing art: " + parent);
			InitializeArt(ArtGenerationContext.Outsider);
		}

		return GenText.CapitalizeAsTitle(taleRef.GenerateText(TextGenerationPurpose.ArtName, Props.nameMaker));
	}
}}
