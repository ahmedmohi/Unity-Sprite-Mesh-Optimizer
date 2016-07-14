using UnityEngine;
using System.Collections;
using UnityEditor;
using System.Collections.Generic;

namespace SpriteMesher
{

public class SpriteMeshOptimizer : AssetPostprocessor
{

    //Set this bool with false if we want to stop the script from working
     bool isWorking = true;
    

    private void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
    {

        if (isWorking)
        {

            if (assetPath.ToUpper().Contains("UI") || assetPath.Contains("Standard Assets")) return;

            var importer = assetImporter as TextureImporter;
            if (importer == null) return;

            if (importer.textureType != TextureImporterType.Sprite && importer.textureType != TextureImporterType.Advanced) return;

            if (importer.textureType == TextureImporterType.Advanced && importer.spriteImportMode == SpriteImportMode.None)
                return;


            foreach (Sprite spriteItem in sprites)
            {
                Vector2[] spritesVertices = new Vector2[spriteItem.vertices.Length];



                //we need to transform from sprite space and compensite for pixels per unit
                for (int i = 0; i < spriteItem.vertices.Length; i++)
                {

                    spritesVertices[i] = new Vector2((spriteItem.vertices[i].x * spriteItem.pixelsPerUnit) + spriteItem.rect.size.x / 2,
                        (spriteItem.vertices[i].y * spriteItem.pixelsPerUnit) + spriteItem.rect.size.y / 2);
                }

                Mesh mesh = new Mesh();

                mesh.vertices = spritesVertices.toVector3Array();
                mesh.triangles = spriteItem.triangles.toIntArray();

                //Generate Mesh
                AutoWeld(mesh, 1000f, 1000f);



                Debug.Log(spriteItem.name + " : Sprite mesh optimized");

                Vector2[] verticesVector2D = mesh.vertices.toVector2Array();

                //Check and make sure no vertices is out of sprite bounds
                for (int i = 0; i < verticesVector2D.Length; i++)
                {
                    if (verticesVector2D[i].x < 0)
                    {
                        verticesVector2D[i] = new Vector2(0, verticesVector2D[i].y);
                    }

                    else if (verticesVector2D[i].x > spriteItem.rect.size.x)
                    {
                        verticesVector2D[i] = new Vector2(spriteItem.rect.size.x, verticesVector2D[i].y);
                    }



                    if (verticesVector2D[i].y < 0)
                    {
                        verticesVector2D[i] = new Vector2(verticesVector2D[i].x, 0);
                    }
                    else if (verticesVector2D[i].y > spriteItem.rect.size.y)
                    {
                        verticesVector2D[i] = new Vector2(verticesVector2D[i].x, spriteItem.rect.size.y);
                    }

                }


                //Add the new mesh to the sprite
                spriteItem.OverrideGeometry(verticesVector2D, mesh.triangles.toUshoartArray());

            }
        }
    }


    /*********************************************************************
     * The code below by awesome guy from  this source
     * http://answers.unity3d.com/questions/228841/dynamically-combine-verticies-that-share-the-same.html
     * *******************************************************************/
    public static void AutoWeld(Mesh mesh, float threshold, float bucketStep)
    {
        Vector3[] oldVertices = mesh.vertices;
        Vector3[] newVertices = new Vector3[oldVertices.Length];
        int[] old2new = new int[oldVertices.Length];
        int newSize = 0;

        // Find AABB
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        for (int i = 0; i < oldVertices.Length; i++)
        {
            if (oldVertices[i].x < min.x) min.x = oldVertices[i].x;
            if (oldVertices[i].y < min.y) min.y = oldVertices[i].y;
            if (oldVertices[i].z < min.z) min.z = oldVertices[i].z;
            if (oldVertices[i].x > max.x) max.x = oldVertices[i].x;
            if (oldVertices[i].y > max.y) max.y = oldVertices[i].y;
            if (oldVertices[i].z > max.z) max.z = oldVertices[i].z;
        }

        // Make cubic buckets, each with dimensions "bucketStep"
        int bucketSizeX = Mathf.FloorToInt((max.x - min.x) / bucketStep) + 1;
        int bucketSizeY = Mathf.FloorToInt((max.y - min.y) / bucketStep) + 1;
        int bucketSizeZ = Mathf.FloorToInt((max.z - min.z) / bucketStep) + 1;
        List<int>[, ,] buckets = new List<int>[bucketSizeX, bucketSizeY, bucketSizeZ];

        // Make new vertices
        for (int i = 0; i < oldVertices.Length; i++)
        {
            // Determine which bucket it belongs to
            int x = Mathf.FloorToInt((oldVertices[i].x - min.x) / bucketStep);
            int y = Mathf.FloorToInt((oldVertices[i].y - min.y) / bucketStep);
            int z = Mathf.FloorToInt((oldVertices[i].z - min.z) / bucketStep);

            // Check to see if it's already been added
            if (buckets[x, y, z] == null)
                buckets[x, y, z] = new List<int>(); // Make buckets lazily

            for (int j = 0; j < buckets[x, y, z].Count; j++)
            {
                Vector3 to = newVertices[buckets[x, y, z][j]] - oldVertices[i];
                if (Vector3.SqrMagnitude(to) < threshold)
                {
                    old2new[i] = buckets[x, y, z][j];
                    goto skip; // Skip to next old vertex if this one is already there
                }
            }

            // Add new vertex
            newVertices[newSize] = oldVertices[i];
            buckets[x, y, z].Add(newSize);
            old2new[i] = newSize;
            newSize++;

        skip: ;
        }

        // Make new triangles
        int[] oldTris = mesh.triangles;
        int[] newTris = new int[oldTris.Length];
        for (int i = 0; i < oldTris.Length; i++)
        {
            newTris[i] = old2new[oldTris[i]];
        }

        Vector3[] finalVertices = new Vector3[newSize];
        for (int i = 0; i < newSize; i++)
            finalVertices[i] = newVertices[i];

        mesh.Clear();
        mesh.vertices = finalVertices;
        mesh.triangles = newTris;
        mesh.RecalculateNormals();
        mesh.Optimize();
    }
}


public static class HelperArrayExtension
{
    public static Vector3[] toVector3Array(this Vector2[] v2)
    {
        return System.Array.ConvertAll<Vector2, Vector3>(v2, getV3fromV2);
    }

    public static Vector3 getV3fromV2(Vector2 v3)
    {
        return new Vector3(v3.x, v3.y,0);
    }


    public static int[] toIntArray(this ushort[] ush)
    {
        return System.Array.ConvertAll<ushort, int>(ush, getInt);
    }

    public static int getInt(ushort u)
    {
        return System.Convert.ToInt32(u);
    }


    public static ushort[] toUshoartArray(this int[] inA)
    {
        return System.Array.ConvertAll<int, ushort>(inA, getushort);
    }

    public static ushort getushort(int u)
    {
        return (ushort)u;
    }


    public static Vector2[] toVector2Array(this Vector3[] v3)
    {
        return System.Array.ConvertAll<Vector3, Vector2>(v3, getV3fromV2);
    }

    public static Vector2 getV3fromV2(Vector3 v3)
    {
        return new Vector2(v3.x, v3.y);
    }
}


}