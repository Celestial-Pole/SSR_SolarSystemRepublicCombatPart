using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.Attachments;

public class SlotPropertyDef : Def
{
	public List<string> tags;

	public new string label;

	public string iconPath;

	public int displayOrder = 1000;

	public int capacity = 1;

	public bool isEssential;

	[Tooltip("定义与此槽位互斥的其他槽位列表(通过defName指定)。只要这个列表中的任何一个槽位被占用，此槽位就会被禁用，反之亦然。")]
	public List<string> mutexSlots;
}
