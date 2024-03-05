#pragma once

#if UNITY_VERSION >= 202310
    #pragma multi_compile_fragment _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2

    #if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
    	#include "Packages/com.unity.render-pipelines.core/Runtime/Lighting/ProbeVolume/ProbeVolume.hlsl"
    #endif
#endif


half3 SampleGI(float3 position, float2 screenUV)
{
	#if UNITY_VERSION >= 202310
		#if defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2)
		    float3 apvDiffuseGI;
		    EvaluateAdaptiveProbeVolume(position, screenUV, apvDiffuseGI);

		    if (AnyIsNaN(apvDiffuseGI))
		        apvDiffuseGI = float3(0, 0, 0);

		    return apvDiffuseGI / 300;
		#endif
	#endif

	// Will get pre-2023 light probe & proxy volume sampling working later when I have the energy.
	return 0;
}