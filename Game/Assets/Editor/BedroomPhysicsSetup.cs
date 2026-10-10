using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CatSimulator.Editor
{
    public static class BedroomPhysicsSetup
    {
        public static void Apply(GameObject room)
        {
            var friction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Generated/BedroomPropFriction.physicMaterial");
            if (friction == null)
            {
                friction = new PhysicsMaterial("BedroomPropFriction") { staticFriction = .48f,
                    dynamicFriction = .36f, bounciness = .02f,
                    frictionCombine = PhysicsMaterialCombine.Average, bounceCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(friction, "Assets/Generated/BedroomPropFriction.physicMaterial");
            }
            var transforms = room.GetComponentsInChildren<Transform>();
            foreach (var geometry in transforms.Where(t => t.name.StartsWith("Bedroom_Prop_", StringComparison.Ordinal)))
            {
                // FBX empties carry a 100x unit conversion. Keep that on the art, while
                // Rigidbody and joint coordinates use an ordinary one-metre Unity root.
                var root = new GameObject(geometry.name).transform;
                root.SetParent(room.transform, true);
                root.position = geometry.position;
                geometry.name += "_Geometry";
                geometry.SetParent(root, true);
                foreach (var child in root.GetComponentsInChildren<Transform>())
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 0);
                foreach (var collider in root.GetComponentsInChildren<MeshCollider>())
                {
                    collider.convex = true;
                    collider.sharedMaterial = friction;
                    collider.contactOffset = .002f;
                }
                var body = root.gameObject.AddComponent<Rigidbody>();
                body.mass = Mass(root.name);
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearDamping = .08f;
                body.angularDamping = .20f;
                body.maxDepenetrationVelocity = 1.5f;
                body.solverIterations = 10;
                body.solverVelocityIterations = 4;
                if (root.name.Contains("Lamp_"))
                {
                    var basePart = root.GetComponentsInChildren<Renderer>().First(r => r.name.Contains("LampBase"));
                    body.centerOfMass = root.InverseTransformPoint(basePart.bounds.center + Vector3.up * .075f);
                }
                root.gameObject.AddComponent<PushableProp>();
            }
            transforms = room.GetComponentsInChildren<Transform>();
            foreach (var pillow in transforms.Where(t => t.name.StartsWith("Bedroom_Pillow", StringComparison.Ordinal)))
            {
                GameObjectUtility.SetStaticEditorFlags(pillow.gameObject, 0);
                pillow.gameObject.AddComponent<SoftPillow>();
            }
            var phone = transforms.First(t => t.name == "Bedroom_Prop_Phone");
            var phoneAnchor = transforms.First(t => t.name == "Bedroom_ChargingPhoneAnchor");
            var wallAnchor = transforms.First(t => t.name == "Bedroom_ChargingWallAnchor");
            var joint = phone.gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = phone.InverseTransformPoint(phoneAnchor.position);
            joint.connectedAnchor = wallAnchor.position;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Limited;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Free;
            joint.linearLimit = new SoftJointLimit { limit = 1.08f, contactDistance = .025f };
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = 2500, damper = 35 };
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .025f;
            var cable = transforms.First(t => t.name == "Bedroom_ChargingCable");
            cable.GetComponent<Renderer>().enabled = false;
            UnityEngine.Object.DestroyImmediate(cable.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(cable.gameObject, 0);
            var line = cable.gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/Trim_White.mat");
            line.widthMultiplier = .0056f;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            var simulation = cable.gameObject.AddComponent<ChargingCable>();
            simulation.wallAnchor = wallAnchor;
            simulation.phoneAnchor = phoneAnchor;
        }

        static float Mass(string name)
        {
            if (name.Contains("Computer")) return 8;
            if (name.Contains("Monitor")) return 2.2f;
            if (name.Contains("Lamp")) return .85f;
            if (name.Contains("Plant")) return .75f;
            if (name.Contains("Keyboard")) return .65f;
            if (name.Contains("Mouse")) return .095f;
            if (name.Contains("Phone")) return .19f;
            if (name.Contains("DeskMat")) return .28f;
            if (name.Contains("Book")) return .32f;
            if (name.Contains("PenCup")) return .23f;
            return .16f;
        }
    }
}
