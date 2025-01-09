# VRM-Animation-Conversion
A small tool to convert animations from an original set of bones to the normalized bone structure of VRM models

## Installation
Since this is just one file there is no need for a unitypackage. Simply copy the file into your Assets and you will find the window in `Tools/Transmute Animations`

## Usage
1. Set the `Donor Animation` to the object responsible for animating the original Model and the `Donor Bone` to the root bone of your Model.
2. Repeat with the respective `Target`s in your VRM model
   - you can check if it has worked successfully by looking at `Mismatched Bones`
3. Select your Folder with the original Assets
   - you can see affected animations under `Assets`
4. Select your Folder where the new Assets will be (`-VRM` will be appended to the name)
5. Select an accuracy, for good results use `0.001` for the Position and `1.5` for the rotation, else `0`
6. Run and wait!
7. You may need to reapply the animations in your VRM model
