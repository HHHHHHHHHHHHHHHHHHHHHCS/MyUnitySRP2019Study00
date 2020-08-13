using System;
using System.Collections;
using System.Collections.Generic;
using MyRenderPipeline.RenderPass.ScreenSpacePlanarReflection;
using UnityEngine;

public class SSPRCtrl : MonoBehaviour
{
	public float rotationSpeed = 22.5f;
	public List<Material> skyboxs = new List<Material>();

	private int skyboxIndex;
	
	private void LateUpdate()
	{
		transform.RotateAround(Vector3.zero, Vector3.up, rotationSpeed * Time.deltaTime);
	}

	private void OnGUI()
	{
		GUI.contentColor = Color.black;
		
		ScreenSpacePlanarReflectionFeature.instance.shouldRenderSSPR = (GUI.Toggle(new Rect(200, 25, 100, 100), ScreenSpacePlanarReflectionFeature.instance.shouldRenderSSPR, "SSPR on"));
        
		GUI.Label(new Rect(350, 25, 200, 25), $"ColorRT Height = {ScreenSpacePlanarReflectionFeature.instance.rt_height}");
        ScreenSpacePlanarReflectionFeature.instance.rt_height = (int)(GUI.HorizontalSlider(new Rect(550, 25, 200, 25), ScreenSpacePlanarReflectionFeature.instance.rt_height/128, 1,8))*128;
        
        ScreenSpacePlanarReflectionFeature.instance.applyFillHoleFix = (GUI.Toggle(new Rect(550, 225, 200, 25), ScreenSpacePlanarReflectionFeature.instance.applyFillHoleFix,"Apply Fill Hole Fix"));

        
        if (GUI.Button(new Rect(200, 200, 100, 100), "SwitchSkyBox"))
        {
	        skyboxIndex = (skyboxIndex + 1) % skyboxs.Count;
	        RenderSettings.skybox = skyboxs[skyboxIndex];
        }
        
        // 表示一个平稳的deltaTime,根据前 N帧的时间加权平均的值
        GUI.Label(new Rect(200, 150, 100, 100), $"{(int)(Time.smoothDeltaTime * 1000)} ms ({ Mathf.CeilToInt(1f/Time.smoothDeltaTime)}fps)", new GUIStyle() { fontSize = 30 } );

        GUI.Label(new Rect(850, 25, 200, 25), $"Rotate speed = {(int)rotationSpeed}");
        rotationSpeed = (int)(GUI.HorizontalSlider(new Rect(1000, 25, 200, 25), rotationSpeed, 0, 45));

	}
}