using System;
using NUnit.Framework;
using UnityEngine;
using EnCare;

namespace EnCare.Tests
{
    [TestFixture]
    public class LocomotionCompensatorTests
    {
        [Test]
        public void LocomotionDelta_TranslatesPointCorrectly()
        {
            Vector3 oldOrigin = new Vector3(10f, 0f, 10f);
            Vector3 newOrigin = new Vector3(10f, 0.35f, 12f); // Climbed a 0.35m step and walked 2m forward
            Quaternion rotation = Quaternion.identity;

            var delta = new BasketLocomotionCompensator.LocomotionDelta(oldOrigin, newOrigin, rotation);

            Vector3 basketPoint = new Vector3(10.2f, 1.0f, 10.5f);
            Vector3 transformed = delta.TransformPoint(basketPoint);

            // Should be exactly translated by (0, 0.35, 2)
            Assert.AreEqual(10.2f, transformed.x, 1e-5f);
            Assert.AreEqual(1.35f, transformed.y, 1e-5f);
            Assert.AreEqual(12.5f, transformed.z, 1e-5f);
        }

        [Test]
        public void LocomotionDelta_RotatesPointAroundPivot()
        {
            Vector3 playerPivot = new Vector3(5f, 0f, 5f);
            Quaternion turn90 = Quaternion.Euler(0f, 90f, 0f);

            var delta = new BasketLocomotionCompensator.LocomotionDelta(playerPivot, playerPivot, turn90);

            // Basket is 1m in front of player (+Z)
            Vector3 basketPos = new Vector3(5f, 1f, 6f);
            Vector3 transformed = delta.TransformPoint(basketPos);

            // After 90 deg clockwise turn around (5, 0, 5), (+Z) becomes (+X): (6, 1, 5)
            Assert.AreEqual(6f, transformed.x, 1e-4f);
            Assert.AreEqual(1f, transformed.y, 1e-4f);
            Assert.AreEqual(5f, transformed.z, 1e-4f);
        }

        [Test]
        public void LocomotionDelta_PreservesRelativeOffsetBetweenBasketAndTrash()
        {
            Vector3 oldPlayer = new Vector3(0f, 0f, 0f);
            Vector3 newPlayer = new Vector3(3f, 0.5f, 4f);
            Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);

            var delta = new BasketLocomotionCompensator.LocomotionDelta(oldPlayer, newPlayer, rotation);

            Vector3 basketPos = new Vector3(0.3f, 1.0f, 0.5f);
            Quaternion basketRot = Quaternion.Euler(10f, 20f, 0f);

            // Trash item inside basket with relative offset
            Vector3 relativeTrashOffset = new Vector3(0.05f, 0.08f, -0.02f);
            Vector3 trashPos = basketPos + basketRot * relativeTrashOffset;

            // Transform both through locomotion delta
            Vector3 newBasketPos = delta.TransformPoint(basketPos);
            Quaternion newBasketRot = delta.TransformRotation(basketRot);
            Vector3 newTrashPos = delta.TransformPoint(trashPos);

            // Relative offset in new basket local space must be IDENTICAL
            Vector3 recoveredOffset = Quaternion.Inverse(newBasketRot) * (newTrashPos - newBasketPos);

            Assert.AreEqual(relativeTrashOffset.x, recoveredOffset.x, 1e-4f);
            Assert.AreEqual(relativeTrashOffset.y, recoveredOffset.y, 1e-4f);
            Assert.AreEqual(relativeTrashOffset.z, recoveredOffset.z, 1e-4f);
        }

        [Test]
        public void LocomotionDelta_RotatesVelocityVectorsWithoutAddingTranslationSpike()
        {
            Vector3 oldOrigin = Vector3.zero;
            Vector3 newOrigin = new Vector3(100f, 50f, 200f); // Massive jump
            Quaternion turn90 = Quaternion.Euler(0f, 90f, 0f);

            var delta = new BasketLocomotionCompensator.LocomotionDelta(oldOrigin, newOrigin, turn90);

            // Item had gentle relative settling velocity forward (+Z at 0.1 m/s)
            Vector3 initialVelocity = new Vector3(0f, -0.2f, 0.1f);
            Vector3 rotatedVelocity = delta.TransformVector(initialVelocity);

            // Magnitude must be exactly preserved
            Assert.AreEqual(initialVelocity.magnitude, rotatedVelocity.magnitude, 1e-5f);

            // Forward (+Z) rotated 90 deg becomes right (+X)
            Assert.AreEqual(0.1f, rotatedVelocity.x, 1e-4f);
            Assert.AreEqual(-0.2f, rotatedVelocity.y, 1e-4f);
            Assert.AreEqual(0f, rotatedVelocity.z, 1e-4f);
        }
    }
}
