# Raymarched Fog Volumes for Unity's Universal RP

Implementation of Raymarched Volumetric Fog in Unity's Universal Render Pipeline

### Features

* Fog Volume Render Feature.
* Fog Volume Profile ScriptableObject.
* Fog Volume Behaviour.

### Usage

* Add this package through the Package Manager using the option at the top left, symbolized by a plus sign. Select the Git URL option at the top left and use the link: [https://github.com/sinnwrig/URP-Fog-Volumes.git].
* Add the Fog Volume Render Feature to the current active renderer.
* Create a new Fog Volume in the scene by adding the FogVolume component to any object.
* Assign a new profile.
* Play with the scale and settings until your fog looks right.

### Potential issues/Requirements

This asset is not backed by heavy testing, and there is a good chance that some featues are incomplete or buggy. However, I am confident of its current state, and that there are no glaring or apparent bugs. 

* Not tested with VR/AR.
* Tested on Linux and Windows machines with Unity 2022. Mac, Mobile, and other platforms are untested.
* Orthographic cameras do not work.

### Limitations

* There is currently a hard cap of 32 lights per volume. 
* Does not use physically based light scattering throughout the volume.
* Temporal Reprojection does not properly reproject skybox.

### Example Scenes
* Outdoors scene with a Directional Light and light shafts
![Outdoors God Rays](Samples~/Scenes/Example-Terrain.png)<br>
* Nighttime scene with four spot lights around a street and building.
![Nighttime Building](Samples~/Scenes/Example-Spotlights.png)<br>
* Nighttime scene with eight spot lights and ground fog around a gas station.
![Nighttime Gas](Samples~/Scenes/Example-GasStation.png)<br>
