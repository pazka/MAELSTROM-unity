# Unity Feed Visualization

This folder contains Unity scripts that replicate the Feed visualization functionality from the original OpenGL implementation.

## Setup Instructions

### 1. Create Unity Project
- Create a new Unity project (3D template recommended)
- Copy all scripts from this folder to your Unity project's Scripts folder

### 4. Create Display Object Prefab
- Create a GameObject with:
  - MeshRenderer component
  - MeshFilter component
  - DisplayObject script
- Assign the material created in step 3
- Save as a prefab

### 5. Setup Scene
1. Create an empty GameObject and name it "FeedManager"
2. Add the following components to FeedManager:
   - `FeedMain` script
   - `DataLoader` script
   - `ObjectPool` script

3. Configure the components:
   - **DataLoader**: Assign your CSV file to the `csvFile` field
   - **ObjectPool**: Assign your display object prefab to the `displayObjectPrefab` field
   - **FeedMain**: 
     - Assign the ObjectPool reference
     - Set `screenSize` (default: 1920x1080)
     - Set `loopDuration` (default: 600 seconds)
     - Set `maxActiveObjects` (default: 1000)

### 6. Camera Setup
- Position your camera to view the visualization
- The objects will be positioned in world space relative to the screen size

## Scripts Overview

### FeedMain.cs
Main controller script that:
- Manages the data flow and object lifecycle
- Creates and destroys DataObjects based on CSV data
- Handles timing and animation
- Provides debug information

### DataLoader.cs
Handles CSV data loading and normalization:
- Loads data from TextAsset (CSV file)
- Normalizes retweet counts using logarithmic scaling
- Normalizes dates to 0-1 range
- Provides data bounds and utility methods

### ObjectPool.cs
Generic object pool for GameObjects:
- Pre-instantiates objects to avoid runtime allocation
- Manages object lifecycle (get/return)
- Configurable pool size limits

### DataObject.cs
Individual data point controller:
- Manages position, velocity, and visual properties
- Handles screen wrapping
- Updates associated display GameObject

### DisplayObject.cs
Visual display handler:
- Manages shader materials and properties
- Creates quad mesh if needed
- Handles material instancing

## Controls

The visualization runs automatically once started. You can modify the following parameters in the inspector:

- **Screen Size**: Size of the virtual screen for positioning
- **Loop Duration**: How long one complete data cycle takes
- **Max Active Objects**: Maximum number of objects displayed simultaneously
- **Debug Info**: Enable/disable console logging

## Customization

### Shader
Modify `FeedShader.shader` to change the visual appearance of data points.

### Data Processing
Modify `DataLoader.cs` to change how CSV data is processed and normalized.

### Object Behavior
Modify `DataObject.cs` to change how individual data points behave (movement, scaling, etc.).

## Performance Notes

- The object pool helps maintain stable performance
- Adjust `maxActiveObjects` based on your target performance
- Consider using object culling for very large datasets
- The logarithmic normalization helps distribute retweet counts more evenly
