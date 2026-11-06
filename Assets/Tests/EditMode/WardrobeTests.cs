using System.Collections.Generic;
using CoinHop.Characters;
using NUnit.Framework;
using UnityEngine;

namespace CoinHop.Tests
{
    public class WardrobeTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void OptionsFor_OptionalSlot_StartsWithNothing()
        {
            // Arrange
            var cap = Part(LpcSlot.Hat, "Cap");
            var wardrobe = new Wardrobe(new[] { cap }, new[] { LpcSlot.Hat });

            // Act
            var options = wardrobe.OptionsFor(LpcSlot.Hat);

            // Assert
            Assert.AreEqual(2, options.Count);
            Assert.IsNull(options[0]);
            Assert.AreSame(cap, options[1]);
        }

        [Test]
        public void OptionsFor_RequiredSlot_HasNoNothingOption()
        {
            // Arrange
            var shirt = Part(LpcSlot.Torso, "Shirt");
            var wardrobe = new Wardrobe(new[] { shirt }, new[] { LpcSlot.Hat });

            // Act
            var options = wardrobe.OptionsFor(LpcSlot.Torso);

            // Assert
            CollectionAssert.AreEqual(new[] { shirt }, options);
            Assert.IsFalse(wardrobe.HasChoice(LpcSlot.Torso));
        }

        [Test]
        public void Step_NextFromLast_WrapsToFirst()
        {
            // Arrange: options sort by name, so the order is A, B
            var a = Part(LpcSlot.Feet, "A Shoes");
            var b = Part(LpcSlot.Feet, "B Boots");
            var wardrobe = new Wardrobe(new[] { b, a }, new LpcSlot[0]);

            // Act
            var next = wardrobe.Step(LpcSlot.Feet, b, +1);

            // Assert
            Assert.AreSame(a, next);
        }

        [Test]
        public void Step_PreviousFromNothing_WrapsToLastPart()
        {
            // Arrange
            var cap = Part(LpcSlot.Hat, "Cap");
            var helm = Part(LpcSlot.Hat, "Helm");
            var wardrobe = new Wardrobe(new[] { cap, helm }, new[] { LpcSlot.Hat });

            // Act
            var previous = wardrobe.Step(LpcSlot.Hat, null, -1);

            // Assert
            Assert.AreSame(helm, previous);
        }

        [Test]
        public void Step_NextFromOnlyPart_ReturnsNothingForOptionalSlot()
        {
            // Arrange
            var cap = Part(LpcSlot.Hat, "Cap");
            var wardrobe = new Wardrobe(new[] { cap }, new[] { LpcSlot.Hat });

            // Act
            var next = wardrobe.Step(LpcSlot.Hat, cap, +1);

            // Assert
            Assert.IsNull(next);
        }

        [Test]
        public void Step_SlotWithoutOptions_KeepsCurrent()
        {
            // Arrange
            var wardrobe = new Wardrobe(new LpcPart[0], new LpcSlot[0]);
            var worn = Part(LpcSlot.Legs, "Pants");

            // Act
            var result = wardrobe.Step(LpcSlot.Legs, worn, +1);

            // Assert
            Assert.AreSame(worn, result);
        }

        private LpcPart Part(LpcSlot slot, string name)
        {
            var part = LpcPart.Create(slot, 0, name, new LpcPart.Sheet[0]);
            _created.Add(part);
            return part;
        }
    }
}
