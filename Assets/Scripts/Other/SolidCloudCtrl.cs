﻿using System;
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
	public Toggle mulRTToggle;
	public Toggle frameRTToggle;
	public Slider frameModeSlider;
	public Text frameModeText;
	public Dropdown rtSizeDropdown;
	public Slider stepSlider;
	public Text stepText;

	private SolidCloudRenderPostProcess settings;

	public void Start()
	{
		volume.profile.TryGet(out settings);

		enableToggle.isOn = settings.enableEffect.value;
		shadowToggle.isOn = settings.sunShadowsStrength.overrideState;
		maskToggle.isOn = settings.enableMask.value;
		blurToggle.isOn = settings.enableBlur.value;
		mulRTToggle.isOn = settings.mulRTBlend.value;
		frameRTToggle.isOn = settings.enableFrame.value;
		frameModeSlider.value = settings.frameMode.value;
		frameModeSlider.minValue = settings.frameMode.min;
		frameModeSlider.maxValue = settings.frameMode.max;
		frameModeText.text = frameModeSlider.value.ToString();
		rtSizeDropdown.value = settings.rtSize.value - 1;
		stepSlider.value = settings.stepping.value;
		stepSlider.minValue = settings.stepping.min;
		stepSlider.maxValue = settings.stepping.max;
		stepText.text = stepSlider.value.ToString();
	}

	private void Update()
	{
		settings.enableEffect.value = enableToggle.isOn;
		settings.sunShadowsStrength.overrideState = shadowToggle.isOn;
		settings.enableMask.value = maskToggle.isOn;
		settings.enableBlur.value = blurToggle.isOn;
		settings.mulRTBlend.value = mulRTToggle.isOn;
		settings.enableFrame.value = frameRTToggle.isOn;
		settings.frameMode.value = (int) frameModeSlider.value;
		frameModeText.text = frameModeSlider.value.ToString();
		settings.rtSize.value = rtSizeDropdown.value + 1;
		settings.stepping.value = stepSlider.value;
		stepText.text = stepSlider.value.ToString();
	}
}