﻿using BepuPhysics;
using BepuPhysics.Collidables;
using BepuUtilities;
using BepuUtilities.Memory;
using Microsoft.Extensions.Logging;
using RenderWareIo;
using SlipeServer.Physics.Assets;
using SlipeServer.Physics.Callbacks;
using SlipeServer.Physics.Entities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace SlipeServer.Physics.Worlds
{
    public class PhysicsWorld : IDisposable
    {
        private readonly BufferPool pool;
        private readonly Simulation simulation;
        private readonly ILogger logger;
        private readonly AssetCollection assetCollection;
        private readonly IThreadDispatcher? threadDispatcher;

        private bool running;
        private int sleepTime;

        public PhysicsWorld(ILogger logger, Vector3 gravity, AssetCollection? assetCollection = null)
        {
            this.logger = logger;
            this.pool = new BufferPool();
            this.simulation = Simulation.Create(this.pool, new NarrowPhaseCallbacks(), new SimplePoseIntegratorCallbacks(gravity), new PositionFirstTimestepper());

            //var targetThreadCount = Math.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            //this.threadDispatcher = new SimpleThreadDispatcher(targetThreadCount);

            this.assetCollection = assetCollection ?? new();
        }

        public void Dispose()
        {
            this.simulation.Dispose();
            GC.SuppressFinalize(this);
        }

        public PhysicsElement<StaticDescription, StaticHandle> AddStatic(IPhysicsMesh mesh, Vector3 position, Quaternion rotation)
        {
            var description = new StaticDescription(position, mesh.MeshIndex, 0.1f);
            description.Pose.Orientation = rotation;
            var handle = this.simulation.Statics.Add(description);
            return new StaticPhysicsElement(handle, description, this.simulation);
        }

        public PhysicsElement<BodyDescription, BodyHandle> AddBody(ConvexPhysicsMesh mesh, Vector3 position, Quaternion rotation, float mass)
        {
            var collidable = new CollidableDescription(mesh.MeshIndex, 0.1f);
            return AddBody(collidable, position, rotation, mass);
        }

        public PhysicsElement<BodyDescription, BodyHandle> AddBody(CompoundPhysicsMesh mesh, Vector3 position, Quaternion rotation, float mass)
        {
            var collidable = new CollidableDescription(mesh.MeshIndex, 0.1f);
            return AddBody(collidable, position, rotation, mass);
        }

        private PhysicsElement<BodyDescription, BodyHandle> AddBody(CollidableDescription collidable, Vector3 position, Quaternion rotation, float mass)
        {
            var pose = new RigidPose(position, rotation);
            var shape = new Sphere(0.25f);
            shape.ComputeInertia(1, out var sphereInertia);
            var inertia = new BodyInertia()
            {
                InverseMass = mass,
                InverseInertiaTensor = new Symmetric3x3()
            };
            var activityDescription = new BodyActivityDescription(0.1f);

            var description = BodyDescription.CreateDynamic(pose, sphereInertia, collidable, activityDescription);
            description.Pose.Orientation = rotation;
            var handle = this.simulation.Bodies.Add(description);
            return new DynamicBodyPhysicsElement(handle, description, this.simulation, this);
        }

        public RayHit? RayCast(Vector3 from, Vector3 direction, float length)
        {
            HitHandler handler = new();
            this.simulation.RayCast(from, direction, length, ref handler);
            return handler.Hit;
        }

        public PhysicsImg LoadImg(string path)
        {
            return new PhysicsImg(path);
        }

        public void Destroy(PhysicsElement<StaticDescription, StaticHandle> element) => this.simulation.Statics.Remove(element.handle);

        public ConvexPhysicsMesh CreateSphere(float radius)
        {
            var sphere = new Sphere(radius);
            var shape = this.simulation.Shapes.Add(sphere);
            return new ConvexPhysicsMesh(sphere, shape);
        }

        public ConvexPhysicsMesh CreateCylinder(float radius, float length)
        {
            var cylinder = new Cylinder(radius, length);
            var shape = this.simulation.Shapes.Add(cylinder);
            return new ConvexPhysicsMesh(cylinder, shape);
        }

        public PhysicsMesh CreateMesh(PhysicsImg imgFile, string dffName)
        {
            var img = imgFile.imgFile.Img;
            var dffFile = new DffFile(img.DataEntries[dffName.ToLower()].Data);
            var dff = dffFile.Dff;

            return CreateMesh(dff);
        }

        public (CompoundPhysicsMesh?, PhysicsMesh?) CreateMesh(PhysicsImg imgFile, string colFileName, string colName)
        {
            var img = imgFile.imgFile.Img;
            var colFile = new ColFile(img.DataEntries[colFileName.ToLower()].Data);
            var col = colFile.Col;
            var combo = col.ColCombos.First(x =>
            {
                var fullString = string.Join("", x.Header.Name);
                var name = fullString.Substring(0, fullString.IndexOf('\0'));
                return name == colName;
            });

            return CreateMesh(combo);
        }

        public PhysicsMesh CreateMesh(RenderWareIo.Structs.Dff.Dff dff)
        {
            return GetPhysicsMesh(GetMeshFromModel(dff));
        }

        public (CompoundPhysicsMesh?, PhysicsMesh?) CreateMesh(RenderWareIo.Structs.Col.ColCombo colCombo)
        {
            var (compound, mesh) = GetMeshFromCollider(colCombo);
            return (
                compound != null ? GetCompoundPhysicsMesh(compound.Value) : (CompoundPhysicsMesh?)null, 
                mesh != null ? GetPhysicsMesh(mesh.Value) : (PhysicsMesh?)null);
        }

        public void Start(int sleepTime)
        {
            if (this.running)
                return;

            this.running = true;
            this.sleepTime = sleepTime;
            Task.Run(StepLoop);
        }

        public void Stop()
        {
            this.running = false;
        }

        public async Task StepLoop()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (this.running)
            {
                try
                {
                    var deltaTime = stopwatch.ElapsedMilliseconds * .0025f;
                    if (deltaTime > 0)
                    {
                        stopwatch.Reset();
                        stopwatch.Start();
                        this.simulation.Timestep(deltaTime, this.threadDispatcher);
                        this.Stepped?.Invoke();
                    }
                    await Task.Delay(this.sleepTime);
                } catch (Exception e)
                {
                    this.logger.LogError(e, $"Physics error: {e.Message}");
                }
            }
        }

        private CompoundPhysicsMesh GetCompoundPhysicsMesh<T>(T mesh) where T : unmanaged, ICompoundShape
        {
            var shape = this.simulation.Shapes.Add(mesh);
            return new CompoundPhysicsMesh(mesh, shape);
        }

        private ConvexPhysicsMesh GetConvexPhysicsMesh<T>(T mesh) where T : unmanaged, IConvexShape
        {
            var shape = this.simulation.Shapes.Add(mesh);
            return new ConvexPhysicsMesh(mesh, shape);
        }

        private PhysicsMesh GetPhysicsMesh<T>(T mesh) where T : unmanaged, IShape
        {
            var shape = this.simulation.Shapes.Add(mesh);
            return new PhysicsMesh(mesh, shape);
        }

        private Mesh GetMeshFromModel(RenderWareIo.Structs.Dff.Dff dff)
        {
            unsafe
            {
                var dffTriangles = dff.Clump.GeometryList.Geometries.First().Triangles;
                var dffVertices = dff.Clump.GeometryList.Geometries.First().MorphTargets.SelectMany(x => x.Vertices).ToArray();

                this.pool.Take(dffTriangles.Count * sizeof(Triangle), out var buffer);
                var triangles = new Buffer<Triangle>(buffer.Memory, dffTriangles.Count);
                int vertexIndex = 0;
                foreach (var triangle in dffTriangles)
                {
                    triangles[vertexIndex++] = new Triangle(
                        dffVertices[triangle.VertexIndexOne],
                        dffVertices[triangle.VertexIndexTwo],
                        dffVertices[triangle.VertexIndexThree]);
                }

                var meshScale = Vector3.One;
                var mesh = new Mesh(triangles, meshScale, this.pool);

                return mesh;
            }
        }

        private (Compound?, Mesh?) GetMeshFromCollider(RenderWareIo.Structs.Col.ColCombo colCombo)
        {
            unsafe
            {
                var colTriangles = colCombo.Body.Faces;
                var colVertices = colCombo.Body.Vertices;

                var shapeCount = colCombo.Body.Spheres.Count + colCombo.Body.Boxes.Count;
                var builder = new CompoundBuilder(this.pool, this.simulation.Shapes, shapeCount);

                Mesh? mesh = null;
                if (colTriangles.Any())
                {
                    this.pool.Take(colTriangles.Count * sizeof(Triangle), out var buffer);
                    var triangles = new Buffer<Triangle>(buffer.Memory, colTriangles.Count);
                    int vertexIndex = 0;
                    foreach (var triangle in colTriangles)
                    {
                        triangles[vertexIndex++] = new Triangle(
                            new Vector3(colVertices[triangle.A].FirstFloat, colVertices[triangle.A].SecondFloat, colVertices[triangle.A].ThirdFloat),
                            new Vector3(colVertices[triangle.B].FirstFloat, colVertices[triangle.B].SecondFloat, colVertices[triangle.B].ThirdFloat),
                            new Vector3(colVertices[triangle.C].FirstFloat, colVertices[triangle.C].SecondFloat, colVertices[triangle.C].ThirdFloat)
                        );
                    }

                    var meshScale = Vector3.One;
                    mesh = new Mesh(triangles, meshScale, this.pool);
                }

                Compound? compound = null;

                if (shapeCount > 0)
                {
                    foreach (var sphere in colCombo.Body.Spheres)
                    {
                        var shape = this.simulation.Shapes.Add(new Sphere(sphere.Radius));
                        builder.Add(shape, new RigidPose(sphere.Center), new Symmetric3x3(), 0);
                    }

                    foreach (var box in colCombo.Body.Boxes)
                    {
                        var size = box.Max - box.Min;
                        var shape = this.simulation.Shapes.Add(new Box(size.X, size.Y, size.Z));
                        builder.Add(shape, new RigidPose(box.Min + size * 0.5f), new Symmetric3x3(), 0);
                    }

                    builder.BuildKinematicCompound(out var children);

                    compound = new Compound(children);
                }

                return (compound, mesh);
            }
        }

        public event Action? Stepped;
    }
}
