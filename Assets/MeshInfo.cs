using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MeshSplitting {
	public class MeshInfo : MonoBehaviour {
		public bool isAnimated;

		private bool ToSplit = false;
		void Update () {
			if (Input.GetKeyUp("q")) {
				ToSplit = true;
			}
			if (ToSplit) {
				MainPart ();
				ToSplit = false;
			}
		}

		void MainPart() {
			//Debug.Log ("split");
			ResetTransform ();
			GetInheritMeshInfo ();
			GetPlaneSpaceMatrix ();
			SortToUpperLower ();
			CreateSeperateParts ();
		}

		private Transform rootTransform;
		private Transform thisObjTransform;
		private GameObject emptyRootObj;
		void ResetTransform(){
			thisObjTransform = gameObject.transform;
			rootTransform = gameObject.transform.root;
			if (thisObjTransform==rootTransform) {
				emptyRootObj = new GameObject ();
				emptyRootObj.name="mesh parts";
				emptyRootObj.transform.parent = rootTransform.parent;
				rootTransform = emptyRootObj.transform;
				thisObjTransform.parent = rootTransform;
			}
		}

		private Mesh inheritMesh;
		private Mesh inheritMeshBaked;
		private List<Vector3> meshVerts;
		private List<Vector3> meshSkinnedPosVerts;
		private List<Vector2> meshUVs;
		private List<Vector3> meshNormals;
		private List<BoneWeight> meshBoneWeights;
		private Matrix4x4[] meshBindPoses;
		private int[] meshTriangles;
		private Renderer inheritMeshRend;
		private SkinnedMeshRenderer inheritMeshSkinnedRend;
		void GetInheritMeshInfo(){
			if (isAnimated) {
				inheritMeshSkinnedRend = gameObject.GetComponent<SkinnedMeshRenderer> ();
				inheritMeshRend = inheritMeshSkinnedRend;
				inheritMesh = gameObject.GetComponent<SkinnedMeshRenderer> ().sharedMesh;
				inheritMeshBaked = new Mesh ();
				gameObject.GetComponent<SkinnedMeshRenderer> ().BakeMesh(inheritMeshBaked);
				meshSkinnedPosVerts = new List<Vector3> (inheritMeshBaked.vertices);
				meshBoneWeights = new List<BoneWeight> ();
				foreach (BoneWeight i in inheritMesh.boneWeights) {
					meshBoneWeights.Add (i);
				}
				meshBindPoses = inheritMesh.bindposes;
			} else {
				inheritMeshRend = gameObject.GetComponent<MeshRenderer> ();
				inheritMesh = gameObject.GetComponent<MeshFilter> ().mesh;
			}
			meshVerts = new List<Vector3> (inheritMesh.vertices);
			meshUVs = new List<Vector2> (inheritMesh.uv);
			meshTriangles = inheritMesh.triangles;
			meshNormals = new List<Vector3> (inheritMesh.normals);
		}


		private Vector3[] triVertWorldToPlanePos;
		private List<int> upperTriangles;
		private List<int> lowerTriangles;
		private int totalVertCount;
		private List<int> upperVertIdx;
		private List<int> lowerVertIdx;
		private List<string> upperVertAdjacency;
		private List<string> lowerVertAdjacency;

		void SortToUpperLower(){
			upperTriangles = new List<int> ();
			lowerTriangles = new List<int> ();
			upperVertIdx = new List<int> ();
			lowerVertIdx = new List<int> ();
			totalVertCount = inheritMesh.vertexCount;
			upperVertAdjacency = new List<string>();
			lowerVertAdjacency = new List<string>();
			for (int i = 0; i < totalVertCount; i++) {
				upperVertAdjacency.Add ("");
				lowerVertAdjacency.Add ("");
			}
			triVertWorldToPlanePos = new Vector3[3];

			int vertIndex;
			int triangleLength = inheritMesh.triangles.Length;
			for (int i=0; i<triangleLength-2; i++){
				if (i % 3 == 0) {
					//calculate tri's 3 vert pos in plane space
					for (int j = 0; j < 3; j++) {
						vertIndex = meshTriangles [i + j];
						Vector3 vertWorldPos;
						if (isAnimated) {
							vertWorldPos = inheritMeshSkinnedRend.localToWorldMatrix.MultiplyPoint3x4(inheritMeshBaked.vertices [vertIndex]);
						} else {
							vertWorldPos = inheritMeshRend.localToWorldMatrix.MultiplyPoint3x4 (inheritMesh.vertices [vertIndex]);
						}
						triVertWorldToPlanePos [j] = M_planeTrans.MultiplyPoint3x4 (vertWorldPos);; 
					}

					//add original vert into upper or lower mesh triangle list; calculate the intersect point with the plane and add that point to vert list and upper and lower triangle list
					upperVertIdx.Clear();
					lowerVertIdx.Clear();
					int jNext;
					int vertNextIndex;
					for (int j=0; j<3; j++){
						jNext = (j + 1) % 3;
						if (triVertWorldToPlanePos [j].y > 0) {
							upperVertIdx.Add (meshTriangles [i + j]);
						} else if (triVertWorldToPlanePos [j].y < 0) {
							lowerVertIdx.Add (meshTriangles [i + j]);
						} else {
							upperVertIdx.Add (meshTriangles [i + j]);
							lowerVertIdx.Add (meshTriangles [i + j]);
						}
						if (triVertWorldToPlanePos [j].y * triVertWorldToPlanePos [jNext].y < 0) {
							Vector3 intersectPointInPlaneSpace = CalculateLineIntersectWithPlane (triVertWorldToPlanePos [j], triVertWorldToPlanePos [jNext]);
							Vector4 intersectPointInWorldSpace = M_planeTransInv.MultiplyPoint3x4(intersectPointInPlaneSpace);
							Vector3 intersectPointInLocalSpace = inheritMeshRend.worldToLocalMatrix.MultiplyPoint3x4 (intersectPointInWorldSpace);
							vertIndex = meshTriangles [i + j];
							vertNextIndex = meshTriangles [i + jNext];
							float k = (intersectPointInLocalSpace - meshVerts [vertIndex]).magnitude/(meshVerts [vertNextIndex] - meshVerts [vertIndex]).magnitude;
							Vector2 intersectPointUVPos = Vector2.Lerp(meshUVs[vertIndex], meshUVs[vertNextIndex], k);
							meshUVs.Add (intersectPointUVPos);
							Vector3 intersectPointNormal = Vector3.Lerp (meshNormals [vertIndex], meshNormals [vertNextIndex], k);
							meshNormals.Add (intersectPointNormal);
							if (isAnimated) {
								meshSkinnedPosVerts.Add (intersectPointInLocalSpace);
								BoneWeight intersectPointBoneWeight = LerpBoneWeight (meshBoneWeights [vertIndex], meshBoneWeights [vertNextIndex], k);
								meshBoneWeights.Add (intersectPointBoneWeight);
								Vector3 unskinnedIntersectPointPos = UnskinnedPos (intersectPointInWorldSpace, intersectPointBoneWeight);
								meshVerts.Add (unskinnedIntersectPointPos);
							} else {
								meshVerts.Add (intersectPointInLocalSpace);
							}
							upperVertIdx.Add (totalVertCount);
							lowerVertIdx.Add (totalVertCount);
							upperVertAdjacency.Add ("");
							lowerVertAdjacency.Add ("");
							totalVertCount++;
						}
					}

					//original vert always occupies VertIdx[0]
					for (int j = 0; j < upperVertIdx.Count - 2; j++) {
						upperTriangles.Add (upperVertIdx [0]);
						upperTriangles.Add (upperVertIdx [j+1]);
						upperTriangles.Add (upperVertIdx [j+2]);
						upperVertAdjacency [upperVertIdx [0]] += upperVertIdx [j + 1] + "," + upperVertIdx [j + 2] + ",";
						upperVertAdjacency [upperVertIdx [j+1]] += upperVertIdx [0] + "," + upperVertIdx [j + 2] + ",";
						upperVertAdjacency [upperVertIdx [j+2]] += upperVertIdx [j + 1] + "," + upperVertIdx [0] + ",";
					}
					for (int j = 0; j < lowerVertIdx.Count - 2; j++) {
						lowerTriangles.Add (lowerVertIdx [0]);
						lowerTriangles.Add (lowerVertIdx [j+1]);
						lowerTriangles.Add (lowerVertIdx [j+2]);
						lowerVertAdjacency [lowerVertIdx [0]] += lowerVertIdx [j + 1] + "," + lowerVertIdx [j + 2] + ",";
						lowerVertAdjacency [lowerVertIdx [j+1]] += lowerVertIdx [0] + "," + lowerVertIdx [j + 2] + ",";
						lowerVertAdjacency [lowerVertIdx [j+2]] += lowerVertIdx [j + 1] + "," + lowerVertIdx [0] + ",";
					}
				}
			}
		}



		public GameObject splitPlane;
		private Matrix4x4 M_translation;
		private Matrix4x4 M_rotationX;
		private Matrix4x4 M_rotationY;
		private Matrix4x4 M_rotationZ;
		//private Matrix4x4 M_scale;
		private Matrix4x4 M_planeTrans;
		private Matrix4x4 M_planeTransInv;

		void GetPlaneSpaceMatrix (){
			Vector3 dir = - splitPlane.transform.position;
			Vector3 rot = - splitPlane.transform.rotation.eulerAngles;

			//translation matrix
			M_translation = Matrix4x4.identity;
			M_translation.m03 = dir.x;
			M_translation.m13 = dir.y;
			M_translation.m23 = dir.z;

			//rotation matrix
			float cosA;
			float sinA;

			sinA = Mathf.Sin (rot.x*Mathf.Deg2Rad);
			cosA = Mathf.Cos (rot.x*Mathf.Deg2Rad);
			M_rotationX = Matrix4x4.identity;
			M_rotationX.m11 = cosA;
			M_rotationX.m12 = -sinA;
			M_rotationX.m21 = sinA;
			M_rotationX.m22 = cosA;

			sinA = Mathf.Sin (rot.y*Mathf.Deg2Rad);
			cosA = Mathf.Cos (rot.y*Mathf.Deg2Rad);
			M_rotationY = Matrix4x4.identity;
			M_rotationY.m00 = cosA;
			M_rotationY.m02 = sinA;
			M_rotationY.m20 = -sinA;
			M_rotationY.m22 = cosA;

			sinA = Mathf.Sin (rot.z*Mathf.Deg2Rad);
			cosA = Mathf.Cos (rot.z*Mathf.Deg2Rad);
			M_rotationZ = Matrix4x4.identity;
			M_rotationZ.m00 = cosA;
			M_rotationZ.m01 = -sinA;
			M_rotationZ.m10 = sinA;
			M_rotationZ.m11 = cosA;

			//actually scale is 1 (not changed)
			//M_scale = Matrix4x4.identity;

			M_planeTrans = M_rotationZ * M_rotationX * M_rotationY * M_translation;
			M_planeTransInv = M_planeTrans.inverse;
		}

		//in plane space, calculate a line (p0, p1 defined) intersect with x-z plane
		Vector3 CalculateLineIntersectWithPlane(Vector3 p0, Vector3 p1){
			float t_constant;
			float x2; float z2;
			//x2=x0+(x1-x0)*t; y2=y0+(y1-y0)*t; z2=z0+(z1-z0)t;
			t_constant = p0.y / (p0.y - p1.y);
			x2 = p0.x + t_constant * (p1.x - p0.x);
			z2 = p0.z + t_constant * (p1.z - p0.z);
			return new Vector3(x2,0,z2);
		}

		//know skinned vert world pos, calculate unskinned vert local pos 
		Vector3 UnskinnedPos(Vector3 skinnedPos, BoneWeight thisBoneWeight){
			int boneIdx0 = thisBoneWeight.boneIndex0;
			int boneIdx1 = thisBoneWeight.boneIndex1;
			int boneIdx2 = thisBoneWeight.boneIndex2; 
			int boneIdx3 = thisBoneWeight.boneIndex3;
			Matrix4x4 boneMatrix0 = inheritMeshSkinnedRend.bones[boneIdx0].localToWorldMatrix * meshBindPoses[boneIdx0];
			Matrix4x4 boneMatrix1 = inheritMeshSkinnedRend.bones[boneIdx1].localToWorldMatrix * meshBindPoses[boneIdx1];
			Matrix4x4 boneMatrix2 = inheritMeshSkinnedRend.bones[boneIdx2].localToWorldMatrix * meshBindPoses[boneIdx2];
			Matrix4x4 boneMatrix3 = inheritMeshSkinnedRend.bones[boneIdx3].localToWorldMatrix * meshBindPoses[boneIdx3];
			float boneWeight0 = thisBoneWeight.weight0;
			float boneWeight1 = thisBoneWeight.weight1;
			float boneWeight2 = thisBoneWeight.weight2;
			float boneWeight3 = thisBoneWeight.weight3;
			Matrix4x4 vertMatrix = new Matrix4x4 ();
			for (int i=0; i<16; i++){
				vertMatrix[i] = boneMatrix0[i]*boneWeight0 + boneMatrix1[i]*boneWeight1+boneMatrix2[i]*boneWeight2+boneMatrix3[i]*boneWeight3;
			}
			Vector3 unskinnedPos;
			unskinnedPos= vertMatrix.inverse.MultiplyPoint3x4(skinnedPos);
			return unskinnedPos;
		}

		BoneWeight LerpBoneWeight(BoneWeight boneWeight0, BoneWeight boneWeight1, float k){
			List<int> boneIndex = new List<int>();
			List<float> boneIndexWeight = new List<float>();
			int[] boneWeight0Index = new int[4];
			boneWeight0Index [0] = boneWeight0.boneIndex0; boneWeight0Index [1] = boneWeight0.boneIndex1; boneWeight0Index [2] = boneWeight0.boneIndex2; boneWeight0Index [3] = boneWeight0.boneIndex3;
			int[] boneWeight1Index = new int[4];
			boneWeight1Index [0] = boneWeight1.boneIndex0; boneWeight1Index [1] = boneWeight1.boneIndex1; boneWeight1Index [2] = boneWeight1.boneIndex2; boneWeight1Index [3] = boneWeight1.boneIndex3;
			float[] boneWeight0Weight = new float[4];
			boneWeight0Weight [0] = boneWeight0.weight0; boneWeight0Weight [1] = boneWeight0.weight1; boneWeight0Weight [2] = boneWeight0.weight2; boneWeight0Weight [3] = boneWeight0.weight3;
			float[] boneWeight1Weight = new float[4];
			boneWeight1Weight [0] = boneWeight1.weight0; boneWeight1Weight [1] = boneWeight1.weight1; boneWeight1Weight [2] = boneWeight1.weight2; boneWeight1Weight [3] = boneWeight1.weight3;

			int tempIdx;
			float tempWeight;
			for (int i = 0; i < 4; i++) {
				for (int j = i+1; j < 4; j++) {
					if (boneWeight0Index [i] == boneWeight1Index [j]) {
						tempIdx = boneWeight1Index [i];
						boneWeight1Index [i] = boneWeight1Index [j];
						boneWeight1Index [j] = tempIdx;
						tempWeight = boneWeight1Weight [i];
						boneWeight1Weight [i] = boneWeight1Weight [j];
						boneWeight1Weight [j] = tempWeight;
						break;
					}
				}
			}

			float lerpWeight;
			for (int i = 0; i < 4; i++) {
				if (boneWeight0Index[i]==boneWeight1Index[i]){
					lerpWeight = Mathf.Lerp (boneWeight0Weight [i], boneWeight1Weight [i], k);
					InsertBoneIdxAndWeightList (boneIndex, boneIndexWeight, boneWeight0Index [i], lerpWeight);
				} else {
					lerpWeight = Mathf.Lerp (boneWeight0Weight [i], 0, k);
					InsertBoneIdxAndWeightList (boneIndex, boneIndexWeight, boneWeight0Index [i], lerpWeight);
					lerpWeight = Mathf.Lerp (0, boneWeight1Weight [i], k);
					InsertBoneIdxAndWeightList (boneIndex, boneIndexWeight, boneWeight1Index [i], lerpWeight);
				}
			}

			float sumWeight = 0;
			for (int i = 0; i < 4; i++) {
				sumWeight += boneIndexWeight [i];
			}
			for (int i = 0; i < 4; i++) {
				boneIndexWeight [i] = boneIndexWeight [i] / sumWeight;
			}

			BoneWeight newBoneWeight = new BoneWeight();
			newBoneWeight.boneIndex0 = boneIndex [0]; newBoneWeight.boneIndex1 = boneIndex [1]; newBoneWeight.boneIndex2 = boneIndex [2]; newBoneWeight.boneIndex3 = boneIndex [3];
			newBoneWeight.weight0 = boneIndexWeight [0]; newBoneWeight.weight1 = boneIndexWeight [1]; newBoneWeight.weight2 = boneIndexWeight [2]; newBoneWeight.weight3 = boneIndexWeight [3];
			return newBoneWeight;
		}

		void InsertBoneIdxAndWeightList(List<int> idxList, List<float> weightList, int idx, float weightValue){
			bool isAdded=false;
			for (int i = 0; i<idxList.Count; i++){
				if (weightList[i]<weightValue) {
					idxList.Insert (i, idx);
					weightList.Insert (i, weightValue);
					isAdded=true;
					break;
				}
			}
			if (!isAdded) {
				idxList.Add (idx);
				weightList.Add (weightValue);
			}
		}

		private bool deleteOriObj;
		void CreateSeperateParts(){
			FindVertOfSamePos ();
			deleteOriObj = true;
			CreateSeperateParts (upperTriangles, true);
			CreateSeperateParts (lowerTriangles, false);
			if (deleteOriObj) {
				Destroy (gameObject);
			}
		}

		public Transform[] mainBodyLocators;
		void AddSprtGameObj(){
			GameObject sprtObj = new GameObject ();
			sprtObj.transform.parent = rootTransform;
			sprtObj.transform.position = thisObjTransform.position;
			sprtObj.transform.rotation = thisObjTransform.rotation;
			sprtObj.transform.localScale = thisObjTransform.localScale;
			sprtObj.name = "meshPiece";

			Mesh newSprtMesh = new Mesh ();
			if (isAnimated) {
				newSprtMesh.vertices = seperateMeshVertSkinnedPos.ToArray ();
			} else {
				newSprtMesh.vertices = seperateMeshVertPos.ToArray ();
			}
			newSprtMesh.uv = seperateMeshUV.ToArray ();
			newSprtMesh.normals = seperateMeshNormal.ToArray ();
			newSprtMesh.triangles = seperateMeshTri.ToArray ();
			newSprtMesh.RecalculateBounds ();
			sprtObj.AddComponent<MeshFilter> ().mesh = newSprtMesh;
			sprtObj.AddComponent<MeshRenderer> ().materials = inheritMeshRend.materials;

			if (mainBodyLocators.Length > 0) {
				bool isMainBody = true;
				for (int r = 0; r < mainBodyLocators.Length; r++) {
					if (!sprtObj.GetComponent<MeshRenderer> ().bounds.Contains (mainBodyLocators [r].position)) {
						isMainBody = false;
					}
				}
				if ((isMainBody) && (isAnimated)) {
					Destroy (sprtObj);
					newSprtMesh.vertices = seperateMeshVertPos.ToArray ();
					newSprtMesh.bindposes = meshBindPoses;
					newSprtMesh.boneWeights = seperateBoneWeight.ToArray ();
					newSprtMesh.RecalculateBounds ();
					gameObject.GetComponent<SkinnedMeshRenderer> ().sharedMesh = newSprtMesh;
					MeshCollider mshCol = gameObject.GetComponent<MeshCollider> ();
					if (mshCol != null) {
						gameObject.GetComponent<MeshCollider> ().sharedMesh = newSprtMesh;
					}
					deleteOriObj = false;
				}
			} 
			if (sprtObj!=null) {
				sprtObj.AddComponent<MeshInfo> ().splitPlane = gameObject.GetComponent<MeshInfo> ().splitPlane;
				sprtObj.GetComponent<MeshInfo> ().mainBodyLocators = new Transform[0];
				sprtObj.GetComponent<MeshInfo> ().isAnimated = false;
				sprtObj.AddComponent<BoxCollider> ();
				sprtObj.AddComponent<Rigidbody> ();
			}
		}

		private List<Vector3> seperateMeshVertPos;
		private List<Vector3> seperateMeshVertSkinnedPos;
		private List<Vector2> seperateMeshUV;
		private List<Vector3> seperateMeshNormal;
		private List<BoneWeight> seperateBoneWeight;
		private List<int> seperateMeshTri;
		private List<string> vertAdjacency;
		private int[] inputIdxToSprt;
		private int sprtMeshVertCount;
		void CreateSeperateParts(List<int> upperOrLowerTris, bool isUpper){
			if (isUpper) {
				vertAdjacency = upperVertAdjacency;
			} else {
				vertAdjacency = lowerVertAdjacency;
			}
			seperateMeshVertPos = new List<Vector3> ();
			seperateMeshVertSkinnedPos = new List<Vector3> ();
			seperateBoneWeight = new List<BoneWeight> ();
			seperateMeshUV = new List<Vector2> ();
			seperateMeshNormal = new List<Vector3> (); 
			seperateMeshTri = new List<int> ();
			inputIdxToSprt = new int[totalVertCount];

			int inputTriLength = upperOrLowerTris.Count;
			for (int i=0; i<inputTriLength; i++){
				sprtMeshVertCount = 0;
				for (int k = 0; k < totalVertCount; k++) {
					inputIdxToSprt [k] = -1;
				}
				FindSeperateParts (upperOrLowerTris[i]);
				if (seperateMeshVertPos.Count > 2) {
					//Debug.Log ("a new mesh: "+ seperateMeshVertPos.Count);
					for (int j = 0; j < inputTriLength - 2; j++) {
						if ((j % 3 == 0)&&
							(inputIdxToSprt[upperOrLowerTris[j]]!=-1)&&
							(inputIdxToSprt[upperOrLowerTris[j+1]]!=-1)&&
							(inputIdxToSprt[upperOrLowerTris[j+2]]!=-1)) {
							seperateMeshTri.Add (inputIdxToSprt[upperOrLowerTris[j]]);
							seperateMeshTri.Add (inputIdxToSprt[upperOrLowerTris[j+1]]);
							seperateMeshTri.Add (inputIdxToSprt[upperOrLowerTris[j+2]]);
							upperOrLowerTris.Remove (upperOrLowerTris[j]);
							upperOrLowerTris.Remove (upperOrLowerTris[j]);
							upperOrLowerTris.Remove (upperOrLowerTris[j]);
							if (j + 3 == i) {
								i -= 3;
							} else if (j + 2 == i) {
								i -= 2;
							} else if (j == i) {
								i -= 1;
							}
							j -= 1;
							inputTriLength -= 3;
						}
					}
					AddSprtGameObj ();	
				}
				seperateMeshVertPos.Clear ();
				seperateMeshVertSkinnedPos.Clear ();
				seperateBoneWeight.Clear ();
				seperateMeshUV.Clear ();
				seperateMeshNormal.Clear ();
				seperateMeshTri.Clear ();
			}
		}

		void FindSeperateParts(int i) {
			string vertAdjStr = vertAdjacency [i];
			int firstComma = vertAdjStr.IndexOf (",");
			if (firstComma > 0) {
				int adjVertIdx = int.Parse (vertAdjStr.Substring (0, firstComma));
				if (inputIdxToSprt [adjVertIdx] == -1) {
					inputIdxToSprt [adjVertIdx] = sprtMeshVertCount;
					seperateMeshVertPos.Add (meshVerts [adjVertIdx]);
					if (isAnimated) {
						seperateMeshVertSkinnedPos.Add (meshSkinnedPosVerts [adjVertIdx]);
						seperateBoneWeight.Add (meshBoneWeights [adjVertIdx]);
					}
					seperateMeshUV.Add (meshUVs [adjVertIdx]);
					seperateMeshNormal.Add (meshNormals [adjVertIdx]);
					sprtMeshVertCount++;
					//Debug.Log ("find an adjacent vert: "+adjVertIdx);
				}
				vertAdjacency [i] = vertAdjStr.Substring (firstComma + 1, vertAdjStr.Length - firstComma - 1);
				FindSeperateParts (adjVertIdx);
				FindSeperateParts (i);
			}
		}
			
		void FindVertOfSamePos(){
			int[] vertIdxOfSamePos = new int[totalVertCount];
			for (int i = 0; i < totalVertCount; i++) {
				vertIdxOfSamePos [i] = i;
			}
			for (int i = 0; i < totalVertCount; i++) {
				if (vertIdxOfSamePos [i] == i) {
					for (int j = i + 1; j < totalVertCount; j++) {
						if ((meshVerts [i] - meshVerts [j]).magnitude < 0.001) {
							if ((upperVertAdjacency [i] != "") || (upperVertAdjacency [j] != "")) {
								upperVertAdjacency [i] += j + ",";
								upperVertAdjacency [j] += i + ",";
							}
							if ((lowerVertAdjacency [i] != "") || (lowerVertAdjacency [j] != "")) {
								lowerVertAdjacency [i] += j + ",";
								lowerVertAdjacency [j] += i + ",";
							}
							vertIdxOfSamePos [j] = i;
						}
					}
				}
			}
		}

	}
}

