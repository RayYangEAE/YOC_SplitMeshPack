# YOC_SplitMeshPack

I'm making an origami style game on a class this semester, and I think it is cool if I can slash the paper object in the game, so I made this repo to share my split mesh script. :D

How to use: 
1. MeshInfo script should be attached to the game object;

a. Drug a plan to the "splitPlane" in inspector, then press "q" to let this plane to split the mesh;

b. If this mesh is animated, plese check "animated";

c. If you want to have a mainbody part which will continue playing animation after the split, add one or more locators(empty game object) under the joints; then drug the locator(s) to mainBodyLocators[] in inspector; if all locator(s) are contained in the bounds of a piece of the object; this piece of game object is the mainbody and it is still "alive" to play the animation;
		
2. ExtrudePaper shader is a geometry shader to make faces have a double-sided look as well as adding some thickness with the cross section. 

a. My script do not create cap for the intersect points on the plane, since objects in my game are paper; instead, I use this shader to give thickness to the paper;

b. To use this shader, please soften edge your model in maya/3dmax, or re-calculate the normals in Unity import settings, to make it have correct normal to inflate the geometry a little bit; and then the shader will make it harden-edge again to have a low-poly look.)
    
How the script works：
1. It calculates the plane matrix, to calculate the intersect points on the plane, sort the vertices to upper or lower parts, record vert adjacency, and use the inverse matrix to calculate the local pos and world pos; (if it is animated, use baked vert pos)

2. If it is animated, bone matrices inverse multiply the intersect points' world pos, to calculate the intersect points' vert local pos;

3. Find out all the verts that share the same position and add their adjacency; and find out all adjacent verts to create a new mesh; (so it could have multiple mesh parts instead of just upper and lower; for example, if you split an "M" in middle, you should have 5 pieces of mesh)

4. Figure out whether this is a mainbody mesh by whether locator(s) are all contained in the mesh renderer bounds.
