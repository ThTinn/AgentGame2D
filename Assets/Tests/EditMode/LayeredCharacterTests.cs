using System.Collections.Generic;
using DungeonOnline.Characters;
using NUnit.Framework;
using UnityEngine;

namespace DungeonOnline.Tests
{
    public class LayeredCharacterTests
    {
        private const int Columns = 3;
        private const int BodyOrder = 10;
        private const int HatOrder = 130;

        private readonly List<Object> _created = new List<Object>();
        private LpcAnimationSet _animations;
        private LayeredCharacter _character;

        [SetUp]
        public void SetUp()
        {
            _animations = Track(ScriptableObject.CreateInstance<LpcAnimationSet>());
            _animations.Entries.Add(new LpcAnimationSet.Entry { Name = "idle", Sheet = "idle", Frames = new[] { 0 }, FramesPerSecond = 1f, Loop = true });
            _animations.Entries.Add(new LpcAnimationSet.Entry { Name = "run", Sheet = "run", Frames = new[] { 0, 1, 2 }, FramesPerSecond = 10f, Loop = true });
            _character = Track(new GameObject("Character")).AddComponent<LayeredCharacter>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void Setup_WithOutfit_DrawsEachSlotAtItsOrder()
        {
            // Arrange
            var body = MakePart(LpcSlot.Body, BodyOrder);
            var hat = MakePart(LpcSlot.Hat, HatOrder);

            // Act
            _character.Setup(_animations, new[] { body, hat });

            // Assert
            Assert.AreEqual(BodyOrder, _character.GetRenderer(LpcSlot.Body).sortingOrder);
            Assert.AreEqual(HatOrder, _character.GetRenderer(LpcSlot.Hat).sortingOrder);
            Assert.AreSame(hat.GetFrame("idle", 0), _character.GetRenderer(LpcSlot.Hat).sprite);
        }

        [Test]
        public void Equip_SameSlot_ReplacesOldPart()
        {
            // Arrange
            var capA = MakePart(LpcSlot.Hat, HatOrder);
            var capB = MakePart(LpcSlot.Hat, HatOrder);
            _character.Setup(_animations, new[] { MakePart(LpcSlot.Body, BodyOrder), capA });

            // Act
            _character.Equip(capB);

            // Assert
            Assert.AreSame(capB, _character.GetPart(LpcSlot.Hat));
            Assert.AreSame(capB.GetFrame("idle", 0), _character.GetRenderer(LpcSlot.Hat).sprite);
        }

        [Test]
        public void Unequip_WornSlot_ClearsSpriteAndRaisesEvent()
        {
            // Arrange
            _character.Setup(_animations, new[] { MakePart(LpcSlot.Body, BodyOrder), MakePart(LpcSlot.Hat, HatOrder) });
            var events = new List<(LpcSlot slot, LpcPart part)>();
            _character.OnPartChanged += (slot, part) => events.Add((slot, part));

            // Act
            _character.Unequip(LpcSlot.Hat);

            // Assert
            Assert.IsNull(_character.GetPart(LpcSlot.Hat));
            Assert.IsNull(_character.GetRenderer(LpcSlot.Hat).sprite);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(LpcSlot.Hat, events[0].slot);
            Assert.IsNull(events[0].part);
        }

        [Test]
        public void Tick_WhileRunning_AllLayersShowSameColumn()
        {
            // Arrange
            var body = MakePart(LpcSlot.Body, BodyOrder);
            var hat = MakePart(LpcSlot.Hat, HatOrder);
            _character.Setup(_animations, new[] { body, hat });
            _character.Play("run");

            // Act: 0.15 s at 10 fps is the second step, column 1
            _character.Tick(0.15f);

            // Assert
            Assert.AreEqual(1, _character.CurrentColumn);
            Assert.AreSame(body.GetFrame("run", 1), _character.GetRenderer(LpcSlot.Body).sprite);
            Assert.AreSame(hat.GetFrame("run", 1), _character.GetRenderer(LpcSlot.Hat).sprite);
        }

        [Test]
        public void SetFacingLeft_True_FlipsEveryLayer()
        {
            // Arrange
            _character.Setup(_animations, new[] { MakePart(LpcSlot.Body, BodyOrder), MakePart(LpcSlot.Hat, HatOrder) });

            // Act
            _character.SetFacingLeft(true);

            // Assert
            Assert.IsTrue(_character.GetRenderer(LpcSlot.Body).flipX);
            Assert.IsTrue(_character.GetRenderer(LpcSlot.Hat).flipX);
        }

        private LpcPart MakePart(LpcSlot slot, int order)
        {
            var sheets = new[] { MakeSheet("idle"), MakeSheet("run") };
            return Track(LpcPart.Create(slot, order, slot.ToString(), sheets));
        }

        private LpcPart.Sheet MakeSheet(string name)
        {
            var frames = new Sprite[Columns];
            for (var i = 0; i < Columns; i++)
            {
                var texture = Track(new Texture2D(1, 1));
                frames[i] = Track(Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero));
            }
            return new LpcPart.Sheet { Name = name, Frames = frames };
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
