using System;
using System.Collections;
using System.Collections.Generic;
using MyRenderPipeline.RenderPass.Cloud.SolidCloud;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class SolidCloudCtrl : MonoBehaviour
{
	public Volume volume;

	public Toggle enableToggle;
	public Toggle shadowToggle;
	public Toggle maskToggle;
	public Toggle blurToggle;
	public Dropdown rtSize;

	private SolidCloudRenderPostProcess settings;

	public void Start()
	{
		volume.profile.TryGet(out settings);

		enableToggle.isOn = settings.enableEffect.value;
		shadowToggle.isOn = settings.sunShadowsStrength.overrideState;
		maskToggle.isOn = settings.enableMask.value;
		blurToggle.isOn = settings.enableBlur.value;
		rtSize.value = GetCurrentRTSize(settings.rtSize.value);
	}

	private void Update()
	{
		settings.enableEffect.value = enableToggle.isOn;
		settings.sunShadowsStrength.overrideState = shadowToggle.isOn;
		settings.enableMask.value = maskToggle.isOn;
		settings.enableBlur.value = blurToggle.isOn;
		settings.rtSize.value = rtSize.value + 1;
	}

	private static int GetCurrentRTSize(int rtSize)
	{
		return rtSize <= 3 ? rtSize - 1 : 2;
	}
}