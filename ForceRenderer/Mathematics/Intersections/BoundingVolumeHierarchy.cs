﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CodeHelpers;

namespace ForceRenderer.Mathematics.Intersections
{
	public class BoundingVolumeHierarchy
	{
		public BoundingVolumeHierarchy(PressedPack pack, IReadOnlyList<AxisAlignedBoundingBox> aabbs, IReadOnlyList<uint> tokens)
		{
			this.pack = pack;
			rootAABB = default;

			if (aabbs.Count != tokens.Count) throw ExceptionHelper.Invalid(nameof(tokens), tokens, $"does not have a matching length with {nameof(aabbs)}");
			if (aabbs.Count == 0) return;

			int[] indices = Enumerable.Range(0, aabbs.Count).ToArray();

			BranchBuilder builder = new BranchBuilder(aabbs, indices);
			BranchBuilder.Node root = builder.Build(); //Parallel building reduces build time by about 4 folds on very large scenes

			int index = 1;

			nodes = new Node[builder.NodeCount];
			nodes[0] = CreateNode(root, out maxDepth);

			rootAABB = root.aabb;

			Node CreateNode(BranchBuilder.Node node, out int depth)
			{
				if (node.IsLeaf)
				{
					depth = 1;
					return Node.CreateLeaf(node.aabb, tokens[node.index]);
				}

				int children = index;
				index += 2;

				nodes[children] = CreateNode(node.child0, out int depth0);
				nodes[children + 1] = CreateNode(node.child1, out int depth1);

				depth = Math.Max(depth0, depth1) + 1;
				return Node.CreateNode(node.aabb, children);
			}
		}

		public readonly AxisAlignedBoundingBox rootAABB;

		readonly PressedPack pack;
		readonly Node[] nodes;
		readonly int maxDepth;

		/// <summary>
		/// Traverses and finds the closest intersection of <paramref name="ray"/> with this BVH.
		/// The intersection is recorded on <paramref name="hit"/>, and only only intersections
		/// that are closer than the initial <paramref name="hit.distance"/> value are tested.
		/// </summary>
		public void GetIntersection(in Ray ray, ref Hit hit)
		{
			if (nodes == null) return;

			if (nodes.Length == 1)
			{
				uint token = nodes[0].token; //If root is the only node/leaf
				pack.GetIntersection(ray, ref hit, token);
			}
			else
			{
				ref readonly Node root = ref nodes[0];
				float distance = root.aabb.Intersect(ray);

				if (distance < hit.distance) Traverse(ray, ref hit);
			}
		}

		unsafe void Traverse(in Ray ray, ref Hit hit)
		{
			int* stack = stackalloc int[maxDepth];
			float* hits = stackalloc float[maxDepth];

			int* next = stack;
			*next++ = 1; //The root's first children is always at one

			while (next != stack)
			{
				int index = *--next;
				if (*--hits >= hit.distance) continue;

				ref readonly Node child0 = ref nodes[index];
				ref readonly Node child1 = ref nodes[index + 1];

				float hit0 = child0.aabb.Intersect(ray);
				float hit1 = child1.aabb.Intersect(ray);

				//Orderly intersects the two children so that there is a higher chance of intersection on the first child.
				//Although the order of leaf intersection is wrong, the performance is actually better than reversing to correct it.

				if (hit0 < hit1)
				{
					if (hit1 < hit.distance)
					{
						if (child1.IsLeaf) pack.GetIntersection(ray, ref hit, child1.token);
						else
						{
							*next++ = child1.children;
							*hits++ = hit1;
						}
					}

					if (hit0 < hit.distance)
					{
						if (child0.IsLeaf) pack.GetIntersection(ray, ref hit, child0.token);
						else
						{
							*next++ = child0.children;
							*hits++ = hit0;
						}
					}
				}
				else
				{
					if (hit0 < hit.distance)
					{
						if (child0.IsLeaf) pack.GetIntersection(ray, ref hit, child0.token);
						else
						{
							*next++ = child0.children;
							*hits++ = hit0;
						}
					}

					if (hit1 < hit.distance)
					{
						if (child1.IsLeaf) pack.GetIntersection(ray, ref hit, child1.token);
						else
						{
							*next++ = child1.children;
							*hits++ = hit1;
						}
					}
				}
			}
		}

		/// <summary>
		/// Returns the number of AABB intersection calculated before a result it determined.
		/// </summary>
		public int GetIntersectionCost(in Ray ray, ref float distance)
		{
			if (nodes == null) return 0;

			ref readonly Node root = ref nodes[0];
			float hit = root.aabb.Intersect(ray);

			if (hit >= distance) return 1;
			distance = hit;

			return GetIntersectionCost(root, ray, ref distance) + 1;
		}

		int GetIntersectionCost(in Node node, in Ray ray, ref float distance)
		{
			if (node.IsLeaf)
			{
				//Now we finally calculate the real intersection
				return pack.GetIntersectionCost(ray, ref distance, node.token);
			}

			ref Node child0 = ref nodes[node.children];
			ref Node child1 = ref nodes[node.children + 1];

			float hit0 = child0.aabb.Intersect(ray);
			float hit1 = child1.aabb.Intersect(ray);

			int cost = 2;

			if (hit0 < hit1) //Orderly intersects the two children so that there is a higher chance of intersection on the first child
			{
				if (hit0 < distance) cost += GetIntersectionCost(in child0, ray, ref distance);
				if (hit1 < distance) cost += GetIntersectionCost(in child1, ray, ref distance);
			}
			else
			{
				if (hit1 < distance) cost += GetIntersectionCost(in child1, ray, ref distance);
				if (hit0 < distance) cost += GetIntersectionCost(in child0, ray, ref distance);
			}

			return cost;
		}

		[StructLayout(LayoutKind.Explicit, Size = 32)] //Size must be under 32 bytes to fit two nodes in one cache line (64 bytes)
		readonly struct Node
		{
			Node(in AxisAlignedBoundingBox aabb, uint token, int children)
			{
				this.aabb = aabb; //AABB is assigned before the last two fields
				this.token = token;
				this.children = children;
			}

			[FieldOffset(0)] public readonly AxisAlignedBoundingBox aabb;

			//NOTE: the AABB is 28 bytes large, but its last 4 bytes are not used and only occupied for SIMD loading
			//So we can overlap the next four bytes onto the AABB and pay extra attention when first assigning the fields

			[FieldOffset(24)] public readonly uint token;   //Token will only be assigned if is leaf
			[FieldOffset(28)] public readonly int children; //Index of first child, second child is right after first

			public bool IsLeaf => children == 0;

			public static Node CreateLeaf(in AxisAlignedBoundingBox aabb, uint token) => new Node(aabb, token, 0);
			public static Node CreateNode(in AxisAlignedBoundingBox aabb, int children) => new Node(aabb, default, children);
		}
	}
}