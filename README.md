# Raymarched Fog Volumes for Unity's Universal RP

Implementation of Raymarched Volumetric Fog in Unity's Universal Render Pipeline

Scenes | Outdoors Light Shafts | URP Samples- Japanese Garden | Nighttime streetlights
:-:|:-:|:-:|:-:|
Fog | ![Outdoors Fog](Samples~/Images/Terrain-Fog.png) | ![Garden Fog](Samples~/Images/Garden-Fog.png) | ![Building Fog](Samples~/Images/Building-Fog.png)
No Fog | ![Outdoors](Samples~/Images/Terrain.png)<br> | ![Garden](Samples~/Images/Garden.png)<br> | ![Building](Samples~/Images/Building.png)<br>

[More Examples](#examples)

## Installation

* Open the package manager and select the _Add package from Git URL_ option found under the top left dropdown.<br>
![From Git URL](Samples~/Images/giturl.png)<br>
* Add this repository git URL in the prompt, using the following link: https://github.com/sinnwrig/URP-Fog-Volumes.git.<br>
![Git Input URL](Samples~/Images/gitinput.png)<br>

## Features

* Half & quarter-resolution rendering with depth-aware upsampling (Incompatible with temporal rendering).
* Temporal Rendering with reprojection (Semi-complete).
* Animated scrolling 3D texture noise
* 4 primitive shapes that support non-uniform scale and rotation:
    * Cube
    * Capsule
    * Sphere
    * Cylinder
* Support for all realtime lights, shadows, and cookies up to a maximum of 32 lights per volume.<br>
    * _Lighting is not entirely physically-based, and instead exposes artistic controls for finer tweaking._<br>
* Support for APV GI in Unity 2023.1+

## Usage

* Add the Fog Volume Render Feature to your renderers.
* Create a new Fog Volume in the scene by adding the FogVolume component to any object or using the options under GameObject/Volumes/
* Create or assign a new profile.
* Adjust object scale and profile settings until you get your desired look.

## Limitations

* Does not support DirectX 9 (Desktop GPUs before 2009~2011) or DirectX 11 9.x (DirectX version specifically for Windows Phone and Microsoft Surface RT).
* Temporal Reprojection does not work in scene view, and only works when in play mode.
* Temporal Reprojection cannot reproject parts where fog is facing the skybox or empty space. This is planned on being fixed. 
* There is currently a hard cap of 32 lights per volume. 
* Does not use physically based light scattering throughout the volume.
* Orthographic cameras do not work. This is being worked on.
* Baked lighting does not work at the moment. This is planned on being fixed.

#### Transparency Handling
Due to how transparent objects are rendered to the depth texture, transparency does not work out-of-the-box with Fog Volumes and requires some additional render feature setup detailed in this [Forum Post](https://forum.unity.com/threads/transparent-shader-problem.1059206/), although a brief tutorial will be provided:
1. Put all the transparent renderers you want affected by fog in a seperate layer (i.e 'TransparentDepth).
2. Remove the layer created just now from the _Transparent Layer Mask_ field in the _Filtering_ section of your renderers. These objects will be rendered by us using the render feature.
3. Add the Render Objects render feature to your renderers **before** the Fog Volume render reature.
4. Set the _Event_ field of Render Objects to _AfterRenderingTransparents_
5. Under _Filters_, set the queue to _Transparent_ and the layer mask to our custom transparent depth layer.
6. Under _Overrides_, override _Depth_ and enable _Write Depth_ and set the _Depth Test_ to _Less Equal_

## Potential Issues

I am confident the the asset in its current state has no glaring or overly apparent issues. 
That said, this asset is not backed by heavy testing, and there is a chance that this package may have bugs or not work on some platforms.<br>

This asset has been tested on:
* OpenGL 4.5.
* DirectX 11.
* DirectX 12.
* Vulkan.
* Unity version 2022.3.
* Windows.
* Linux.

This asset has _not_ been tested, but may work on:
* Metal.
* VR or AR platforms.
* Versions of Unity below 2022.3.
* macOS. 
* PlayStation devices.
* Xbox devices.
* Nintendo devices.
* iOS.
* visionOS.
* Android.
* WebGL.
* UWP.
* tvOS.

# Examples
* Japanese Forest with/without fog
![Outdoors Fog](Samples~/Images/Terrain-Fog.png)
![Outdoors](Samples~/Images/Terrain.png)<br>
* Oasis with/without fog
![Oasis Fog](Samples~/Images/Oasis-Fog.png)
![Oasis](Samples~/Images/Oasis.png)<br>
* Building with/without fog
![Building Fog](Samples~/Images/Building-Fog.png)
![Building](Samples~/Images/Building.png)<br>
* Japanese Garden with/without fog
![Garden Fog](Samples~/Images/Garden-Fog.png)
![Garden](Samples~/Images/Garden.png)<br>
* Gas station with/without fog
![Gas Station Fog](Samples~/Images/GasStation-Fog.png)
![Gas Station](Samples~/Images/GasStation.png)<br>
* Demo Terminal Building with fog
![Terminal Fog](Samples~/Images/Terminal-Fog.png)<br>
