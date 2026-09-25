using UnityEngine;

namespace EnCare
{
    /// <summary>
    /// Keeps a deposited trash item riding with the basket without being parented to it.
    /// This avoids compound-Rigidbody physics issues (jitter, floating, getting stuck)
    /// that arise when kinematic children are added to a grabbed dynamic Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BasketPassenger : MonoBehaviour
    {
        Transform m_Basket;
        Vector3 m_LocalOffset;
        Quaternion m_RotationOffset;

        /// <summary>
        /// Begin tracking the basket. Freezes this item's physics.
        /// </summary>
        public void Attach(Transform basket)
        {
            m_Basket = basket;
            m_LocalOffset = basket.InverseTransformPoint(transform.position);
            m_RotationOffset = Quaternion.Inverse(basket.rotation) * transform.rotation;

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
        }

        /// <summary>
        /// Detach from the basket and re-enable physics so the item tumbles out.
        /// </summary>
        public void Release()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.useGravity = true;
                // Small random outward push so items visibly spill out
                rb.AddForce(Vector3.up * 0.8f + Random.insideUnitSphere * 0.4f, ForceMode.Impulse);
            }

            m_Basket = null;
            Destroy(this); // Remove the passenger component, item becomes independent
        }

        void LateUpdate()
        {
            if (m_Basket == null)
            {
                // Basket was destroyed; release gracefully
                Release();
                return;
            }

            transform.position = m_Basket.TransformPoint(m_LocalOffset);
            transform.rotation = m_Basket.rotation * m_RotationOffset;
        }
    }
}
