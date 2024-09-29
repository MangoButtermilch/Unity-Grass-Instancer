#ifndef QUAT_MAT_UTILS
#define QUAT_MAT_UTILS

//Creates TRS matrix. Unity uses column major 
float4x4 CreateTRSMatrix(float3 translation, float4 quaternionRotation, float3 scale) {
    // scale matrix
    float4x4 scaleMatrix = float4x4(
        scale.x, 0, 0, 0,
        0, scale.y, 0, 0,
        0, 0, scale.z, 0,
        0, 0, 0, 1
    );

    // rotation mat from quaternion
    float x2 = quaternionRotation.x + quaternionRotation.x;
    float y2 = quaternionRotation.y + quaternionRotation.y;
    float z2 = quaternionRotation.z + quaternionRotation.z;
    
    float xx = quaternionRotation.x * x2;
    float yy = quaternionRotation.y * y2;
    float zz = quaternionRotation.z * z2;
    float xy = quaternionRotation.x * y2;
    float xz = quaternionRotation.x * z2;
    float yz = quaternionRotation.y * z2;
    float wx = quaternionRotation.w * x2;
    float wy = quaternionRotation.w * y2;
    float wz = quaternionRotation.w * z2;

    float4x4 rotationMatrix = float4x4(
        1.0f - (yy + zz), xy - wz, xz + wy, 0.0f,
        xy + wz, 1.0f - (xx + zz), yz - wx, 0.0f,
        xz - wy, yz + wx, 1.0f - (xx + yy), 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f
    );

    // translation matrix
    float4x4 translationMatrix = float4x4(
        1, 0, 0, translation.x,
        0, 1, 0, translation.y,
        0, 0, 1, translation.z,
        0, 0, 0, 1
    );

    return mul(translationMatrix, mul(rotationMatrix, scaleMatrix));
}

float4 EulerToQuaternion(float pitch, float yaw, float roll) {
    float pitchRad = radians(pitch);
    float yawRad = radians(yaw);
    float rollRad = radians(roll);

    float cy = cos(yawRad * 0.5);
    float sy = sin(yawRad * 0.5);
    float cp = cos(pitchRad * 0.5);
    float sp = sin(pitchRad * 0.5);
    float cr = cos(rollRad * 0.5);
    float sr = sin(rollRad * 0.5);

    float4 q;
    q.x = sr * cp * cy - cr * sp * sy;
    q.y = cr * sp * cy + sr * cp * sy;
    q.z = cr * cp * sy - sr * sp * cy;
    q.w = cr * cp * cy + sr * sp * sy;

    return q;
}

float4 FromToRotation(float3 from, float3 to)
{
    float3 v1 = normalize(from);
    float3 v2 = normalize(to);
    
    float3 axis = cross(v1, v2);
    float angle = acos(dot(v1, v2));
    
    return float4(axis * sin(angle * 0.5), cos(angle * 0.5));
}


// Quaternion multiplication
// http://mathworld.wolfram.com/Quaternion.html
float4 qmul(float4 q1, float4 q2)
{
    return float4(
        q2.xyz * q1.w + q1.xyz * q2.w + cross(q1.xyz, q2.xyz),
        q1.w * q2.w - dot(q1.xyz, q2.xyz)
    );
}

#endif