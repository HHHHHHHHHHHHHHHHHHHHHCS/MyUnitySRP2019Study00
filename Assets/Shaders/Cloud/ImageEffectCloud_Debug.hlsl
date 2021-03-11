#ifndef __ImageEffectCloud_Debug__
#define __ImageEffectCloud_Debug__

// #define DEBUG_MODE 1

#if !DEBUG_MODE
    #define DrawDebugView(uv)
#else
int _DebugViewMode; // 0 = off; 1 = shape tex; 2 = detail tex; 3 = weathermap
int _DebugGreyscale;
int _DebugShowAllChannels;
float _DebugNoiseSliceDepth;
float4 _DebugChannelWeight;
float _DebugTileAmount;
float _ViewerSize;

float4 DebugDrawNoise(float2 uv)
{
    float4 channels = 0;
    float3 samplePos = float3(uv.x, uv.y, _DebugNoiseSliceDepth);

    if (_DebugViewMode == 1)
    {
        channels = _NoiseTex.SampleLevel(sampler_NoiseTex, samplePos, 0);
    }
    else if (_DebugViewMode == 2)
    {
        channels = _DetailNoiseTex.SampleLevel(sampler_DetailNoiseTex, samplePos, 0);
    }
    else if (_DebugViewMode == 3)
    {
        channels = _WeatherMap.SampleLevel(sampler_WeatherMap, samplePos.xy, 0);
    }

    if (_DebugShowAllChannels)
    {
        return channels;
    }
    else
    {
        float4 maskedChannels = (channels * _DebugChannelWeight);
        if (_DebugGreyscale || _DebugChannelWeight.w == 1)
        {
            return dot(maskedChannels, 1);
        }
        else
        {
            return maskedChannels;
        }
    }
}

bool _DrawDebugView(float2 uv, out float4 outCol)
{
    outCol = 0;
    if (_DebugViewMode != 0)
    {
        float width = _ScreenParams.x;
        float height = _ScreenParams.y;
        float minDim = min(width, height);
        float x = uv.x * width;
        float y = (1 - uv.y) * height;

        if (x < minDim * _ViewerSize && y < minDim * _ViewerSize)
        {
            outCol = DebugDrawNoise(float2(x / (minDim * _ViewerSize) * _DebugTileAmount,
                                           y / (minDim * _ViewerSize) * _DebugTileAmount));
            return true;
        }
        return false;
    }
    return false;
}

#define DrawDebugView(uv) \
    float4 outCol; \
    if(_DrawDebugView(uv,outCol)) \
        return outCol; 

#endif


#endif //__ImageEffectCloud_Debug__
