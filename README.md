# Raymarched Fog Volumes for Unity's Universal RP

Implementation of Raymarched Volumetric Fog in Unity's Universal Render Pipeline

### Installation

To add this package to your Unity project, open the Package Manager and add this package using the Git URL option at the top left. Use the link: [https://github.com/sinnwrig/URP-Fog-Volumes.git].

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

* Not tested with VR/AR.
* Tested on Linux and Windows machines with Unity 2022. Mac, Mobile, and other platforms are untested.
* Orthographic cameras do not work.

### Limitations

* There is currently a hard cap of 32 lights per volume. 
* Fog is strictly additive and cannot darken the scene. Multiplicative fog is planned to be added in the future to allow fog to darken the scene.
* Does not use physically based light scattering throughout the volume. Light-marching is planned to be added in the future.
* Temporal Reprojection does not properly reproject from motion vectors. Function motion vector reprojection is planned to be added in the future.
