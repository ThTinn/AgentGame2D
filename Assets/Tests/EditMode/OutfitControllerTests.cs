using System.Collections.Generic;
using CoinHop.Characters;
using NUnit.Framework;
using UnityEngine;

namespace CoinHop.Tests
{
    public class OutfitControllerTests
    {
        private readonly List<Object> _created = new List<Object>();
        private LayeredCharacter _character;
        private OutfitController _outfit;
        private LpcPart _cap;

        [SetUp]
        public void SetUp()
        {
            var animations = Track(ScriptableObject.CreateInstance<LpcAnimationSet>());
            animations.Entries.Add(new LpcAnimationSet.Entry { Name = "idle", Sheet = "idle", Frames = new[] { 0 }, FramesPerSecond = 1f });

            var body = Track(LpcPart.Create(LpcSlot.Body, 10, "Body", new LpcPart.Sheet[0]));
            _cap = Track(LpcPart.Create(LpcSlot.Hat, 130, "Cap", new LpcPart.Sheet[0]));

            var catalog = Track(ScriptableObject.CreateInstance<LpcWardrobeCatalog>());
            catalog.Parts = new List<LpcPart> { body, _cap };
            catalog.OptionalSlots = new List<LpcSlot> { LpcSlot.Hat };

            var go = Track(new GameObject("Player Visual"));
            _character = go.AddComponent<LayeredCharacter>();
            _character.Setup(animations, new[] { body, _cap });
            _outfit = go.AddComponent<OutfitController>();
            _outfit.Setup(_character, catalog);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void Cycle_HatPastLastPart_TakesHatOffAndReports()
        {
            // Arrange
            var events = new List<(LpcSlot slot, LpcPart part)>();
            _outfit.OnOutfitChanged += (slot, part) => events.Add((slot, part));

            // Act
            _outfit.Cycle(LpcSlot.Hat, +1);

            // Assert
            Assert.IsNull(_outfit.Current(LpcSlot.Hat));
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual((LpcSlot.Hat, (LpcPart)null), events[0]);
        }

        [Test]
        public void Cycle_HatTwice_PutsHatBackOn()
        {
            // Act
            _outfit.Cycle(LpcSlot.Hat, +1);
            _outfit.Cycle(LpcSlot.Hat, +1);

            // Assert
            Assert.AreSame(_cap, _outfit.Current(LpcSlot.Hat));
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
