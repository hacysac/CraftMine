using UnityEngine;
using System.Collections.Generic;
public static class Structure
{
    public static Queue<VoxelMod> MakeTree (Vector3 position, int minTrunkHeight, int maxTrunkHeight, World world)
    {
        Queue<VoxelMod> queue = new Queue<VoxelMod>();
        int height = (int) (maxTrunkHeight * Noise.Get2DPerlin(new Vector2(position.x, position.z), 600f, 3f));
        if (height < minTrunkHeight)
        {
            height = minTrunkHeight;
        }
        // Canopy from the top down: a 5-block plus, a 3x3 layer, then two 5x5 layers.
        // The trunk is enqueued last so its logs overwrite the leaves in the centre column.
        for (int x = -2; x < 3; x++)
        {
            for (int z = -2; z < 3; z++)
            {
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height - 2, position.z + z), "Oak Leaves", world));
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height - 3, position.z + z), "Oak Leaves", world));
            }
        }

        for (int x = -1; x < 2; x++)
        {
            for (int z = -1; z < 2; z++)
            {
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height - 1, position.z + z), "Oak Leaves", world));
            }
        }
        for (int x = -1; x < 2; x++)
        {
            if(x==0)
                for (int z = -1; z < 2; z++)
                {
                    queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height, position.z + z), "Oak Leaves", world));
                }
            else
                queue.Enqueue(new VoxelMod(new Vector3(position.x + x, position.y + height, position.z), "Oak Leaves", world));
        }
        for (int i = 1; i < height; i++)
        {
            queue.Enqueue(new VoxelMod(new Vector3(position.x, position.y+i, position.z), "Oak Log", world));
        }

        return queue;
    }
}
