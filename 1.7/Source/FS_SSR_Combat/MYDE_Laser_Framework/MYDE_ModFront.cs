using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MYDE_Laser_Framework;

public static class MYDE_ModFront
{
	public static Vector3 GetVector3_By_AngleFlat(Vector3 Center, float Range, float Angle)
	{
		Vector3 vector = default(Vector3);
		float x = Center.x;
		float z = Center.z;
		float x2 = x - Range * (float)Math.Sin((double)Angle * Math.PI / 180.0);
		float z2 = z - Range * (float)Math.Cos((double)Angle * Math.PI / 180.0);
		return new Vector3(x2, Center.y, z2);
	}

	public static List<IntVec3> GetPos_Square(IntVec3 TargetPos, int CX, int CY)
	{
		List<IntVec3> list = new List<IntVec3>();
		for (int i = -CX; i <= CX; i++)
		{
			for (int j = -CY; j <= CY; j++)
			{
				IntVec3 item = TargetPos + new IntVec3(i, 0, j);
				list.Add(item);
			}
		}
		return list;
	}
}
