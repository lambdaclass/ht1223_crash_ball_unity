using System;
using System.Collections.Generic;
using Communication.Protobuf;
using UnityEngine;

public class ClientPrediction
{
    public struct PlayerInput
    {
        public float joystick_x_value;
        public float joystick_y_value;
        public long timestamp;
    }

    public List<PlayerInput> pendingPlayerInputs = new List<PlayerInput>();

    public void putPlayerInput(PlayerInput PlayerInput)
    {
        pendingPlayerInputs.Add(PlayerInput);
    }

    public void simulatePlayerState(OldPlayer player, long timestamp)
    {
        removeServerAcknowledgedInputs(player, timestamp);
        simulatePlayerMovement(player);
    }

    void removeServerAcknowledgedInputs(OldPlayer player, long timestamp)
    {
        pendingPlayerInputs.RemoveAll((input) => input.timestamp <= timestamp);
    }

    void simulatePlayerMovement(OldPlayer player)
    {
        var characterSpeed = player.Speed;

        pendingPlayerInputs.ForEach(input =>
        {
            Vector2 movementDirection = new Vector2(
                -input.joystick_y_value,
                input.joystick_x_value
            );

            movementDirection.Normalize();
            Vector2 movementVector = movementDirection * characterSpeed;

            var newPositionX = (long)player.Position.X + (long)Math.Round(movementVector.x);
            var newPositionY = (long)player.Position.Y + (long)Math.Round(movementVector.y);

            OldPosition newPlayerPosition = new OldPosition();

            newPlayerPosition.X = (ulong)newPositionX;
            newPlayerPosition.Y = (ulong)newPositionY;

            player.Position = newPlayerPosition;
        });

        var radius = 4900;
        OldPosition center = new OldPosition() { X = 5000, Y = 5000 };

        if (distance_between_positions(player.Position, center) > radius)
        {
            var angle = angle_between_positions(center, player.Position);

            player.Position.X = (ulong)(radius * Math.Cos(angle) + 5000);
            player.Position.Y = (ulong)(radius * Math.Sin(angle) + 5000);
        }
    }

    double distance_between_positions(OldPosition position_1, OldPosition position_2)
    {
        double p1_x = position_1.X;
        double p1_y = position_1.Y;
        double p2_x = position_2.X;
        double p2_y = position_2.Y;

        double distance_squared = Math.Pow(p1_x - p2_x, 2) + Math.Pow(p1_y - p2_y, 2);

        return Math.Sqrt(distance_squared);
    }

    double angle_between_positions(OldPosition center, OldPosition target)
    {
        double p1_x = center.X;
        double p1_y = center.Y;
        double p2_x = target.X;
        double p2_y = target.Y;

        var x_diff = p2_x - p1_x;
        var y_diff = p2_y - p1_y;
        return Math.Atan2(y_diff, x_diff);
    }
}
