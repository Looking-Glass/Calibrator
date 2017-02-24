using System.Collections;
using System.Collections.Generic;
using UnityEngine;



namespace hypercube
{

    public class vertexCalibratorUndoManager 
    {
        public const uint undoCount = 25;

        List<Vector2[,,]> undoQueue = new List<Vector2[,,]>();

        int currentPosition;

        public void clear()
        {
            undoQueue = new List<Vector2[,,]>();
            System.GC.Collect();
        }

        public void recordUndo(Vector2[,,] u)
        {
            //if we have made some undo's, the ones that we have undone should be removed before adding the new undo
            if (undoQueue.Count - (currentPosition + 1) > 0)
                undoQueue.RemoveRange(currentPosition + 1, undoQueue.Count - (currentPosition + 1));

            undoQueue.Add(deepCopyVertices(u));

            //get rid of too many undo's
            while (undoQueue.Count > undoCount)
                undoQueue.RemoveAt(0);
                  
            currentPosition = undoQueue.Count - 1; //set our position to the most current
        }

        public Vector2[,,] undo()
        {
            currentPosition--;
            if (currentPosition < 0)
                currentPosition = 0;

            if (currentPosition >= undoQueue.Count || undoQueue.Count == 0)
                return null;
            
            return deepCopyVertices(undoQueue[currentPosition]);
        }

        public Vector2[,,] redo()
        {
            currentPosition++;
            if (currentPosition >= undoQueue.Count)
                currentPosition = undoQueue.Count - 1;

            if (currentPosition < 0 || undoQueue.Count == 0)
                return null;

            return deepCopyVertices(undoQueue[currentPosition]);
        }

        public static Vector2[,,] deepCopyVertices(Vector2[,,] c)
        {
            int sz = c.GetLength(0);
            int sx = c.GetLength(1);
            int sy = c.GetLength(2);

            Vector2[,,] output = new Vector2[sz, sx, sy];
            for (int z = 0; z < sz; z++)
            {
                for (int y = 0; y < sy; y++)
                {
                    for (int x = 0; x < sx; x++)
                    {
                        output[z, x, y] = c[z, x, y];
                    }
                }
            }
            return output;
        }
            
    }
}
