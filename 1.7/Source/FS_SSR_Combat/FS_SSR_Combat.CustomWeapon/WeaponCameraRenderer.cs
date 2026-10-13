using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FS_SSR_Combat.CustomWeapon;

public class WeaponCameraRenderer : MonoBehaviour
{
	private static Camera weaponCamera;

	private static WeaponCameraRenderer instance;

	private IEnumerable<WeaponRenderRequest> requests;

	public static void Initialize()
	{
		if (!(instance != null))
		{
			GameObject gameObject = new GameObject("SSR_WeaponRenderCamera");
			Object.DontDestroyOnLoad(gameObject);
			gameObject.SetActive(value: false);
			weaponCamera = gameObject.AddComponent<Camera>();
			weaponCamera.transform.position = new Vector3(0f, 15f, 0f);
			weaponCamera.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
			weaponCamera.orthographic = true;
			weaponCamera.orthographicSize = 5f;
			weaponCamera.cullingMask = 0;
			weaponCamera.clearFlags = CameraClearFlags.Color;
			weaponCamera.backgroundColor = Color.clear;
			weaponCamera.useOcclusionCulling = false;
			weaponCamera.renderingPath = RenderingPath.Forward;
			weaponCamera.nearClipPlane = 1f;
			weaponCamera.farClipPlane = 20f;
			instance = gameObject.AddComponent<WeaponCameraRenderer>();
		}
	}

	public static void Render(RenderTexture renderTexture, IEnumerable<WeaponRenderRequest> renderRequests, float orthoSize)
	{
		if (weaponCamera == null || instance == null)
		{
			Log.Error("[SSR_Combat] WeaponCameraRenderer is not initialized!");
			return;
		}
		if (renderTexture == null)
		{
			Log.Warning("[SSR_Combat] Render target is null!");
			return;
		}
		weaponCamera.orthographicSize = orthoSize;
		weaponCamera.aspect = (float)renderTexture.width / (float)renderTexture.height;
		weaponCamera.targetTexture = renderTexture;
		instance.requests = renderRequests;
		weaponCamera.Render();
		weaponCamera.targetTexture = null;
		instance.requests = null;
	}

	public void OnPostRender()
	{
		if (requests == null)
		{
			return;
		}
		Mesh plane = MeshPool.plane10;
		foreach (WeaponRenderRequest request in requests)
		{
			if (request.Material != null)
			{
				request.Material.SetPass(0);
				Graphics.DrawMeshNow(plane, request.Matrix);
			}
		}
	}
}
