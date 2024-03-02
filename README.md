# Raymarched Fog Volumes for Unity's Universal RP

Implementation of Raymarched Volumetric Fog in Unity's Universal Render Pipeline

### Installation



### Features
* Fog Volume Render Feature.
* Atmospheric Scattering Settings ScriptableObject.
* Atmospheric Scattering Effect Behaviour.
* Modified to work with Baked Optical Depth, inspired by Sebastian Lague's video on Atmospheric Rendering found here: https://www.youtube.com/watch?v=DxfEbulyFcY.

### Usage

* Download the complete project repository from GitHub or download only the UnityPackage found in Assets/Atmosphere
* Add the Atmosphere Render Feature to the current active renderer.
* Create a new AtmosphereSettings scriptableObject by right-clicking/Create/Atmosphere/Atmosphere Profile.
* Add an AtmosphereEffect component to an object in your scene.
* Assign the Atmosphere Settings created earlier to the Atmosphere Effect component.
* Tweak the planet/ocean radius and atmosphere scale to appropriate values. Use the example scene as reference for working values.

There is currently no hard limit on amount of active effects allowed in any given scene, but it is best to reduce the amount as much as possible

### Optional

If the scene is using a URP camera stack with the explicit purpose of increasing view distance/maintaining depth precision:
* Add the Depth Stack render feature to your current active renderer.
* Make sure your overlay camera is set to clear depth.
* Atmosphere will automatically use the far camera's depth buffer when needed, increasing the effect's render distance.

### Potential issues/Requirements
* Not tested with VR/AR.
* Earlier versions of URP have shown issues with the Depth Stack not working properly.
* Requires compute shader support on active platform.
* Attempts to pre-bake Optical Depth values into Texture3D's on the CPU did not work in shader.
* Tested on Linux and Windows machines with Unity 2022. Mac, Mobile, and other platforms are untested.
* Orthographic cameras do not work.

### Limitations
* Each active effect supports only one main light. Can be modified to use more lights, potentially for multiple suns/moons.
