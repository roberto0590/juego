using System.Collections.Generic;
using GodGame.Domain;
using GodGame.Rules;
using GodGame.Rules.Assets;
using NUnit.Framework;
using UnityEngine;

namespace GodGame.Tests
{
    /// <summary>Tests de los ScriptableObjects de reglas (solo en el Test Runner de Unity: usan UnityEngine).</summary>
    public class UniverseRulesAssetTests
    {
        private readonly List<UnityEngine.Object> _creados = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object creado in _creados)
                if (creado != null) UnityEngine.Object.DestroyImmediate(creado);
            _creados.Clear();
        }

        [Test]
        public void ToRuntimeData_DevuelveCopia()
        {
            UniverseRules asset = Crear<UniverseRules>();
            UniverseRulesData origen = TestRules.Make("prueba", gravity: 0.6f);
            var presentacion = new UniversePresentationDefaults { orbitWobble = 0.1f };
            asset.Initialize(origen, presentacion);

            // Initialize guarda copias: cambiar los originales no toca el asset.
            origen.gravityMultiplier = 2f;
            presentacion.orbitWobble = 0.5f;
            Assert.AreEqual("prueba", asset.Id);
            Assert.AreEqual(0.6f, asset.Data.gravityMultiplier, 1e-6f);
            Assert.AreEqual(0.1f, asset.Presentation.orbitWobble, 1e-6f);

            // ToRuntimeData devuelve una copia profunda: modificarla no cambia el asset.
            UniverseRulesData copia = asset.ToRuntimeData();
            Assert.IsFalse(ReferenceEquals(copia, asset.Data));
            Assert.AreEqual(0.6f, copia.gravityMultiplier, 1e-6f);

            copia.gravityMultiplier = 1.5f;
            copia.modifiers.miracleCost = 3f;
            copia.nativeSpeciesIds[0] = "otra";
            copia.spectralWeights[0] = 9f;

            Assert.AreEqual(0.6f, asset.Data.gravityMultiplier, 1e-6f);
            Assert.AreEqual(1f, asset.Data.modifiers.miracleCost, 1e-6f);
            Assert.AreEqual("test_species", asset.Data.nativeSpeciesIds[0]);
            Assert.AreEqual(0.01f, asset.Data.spectralWeights[0], 1e-6f);
        }

        [Test]
        public void Catalogo_BuildRuntimeRules_OrdenaYOmiteNulos()
        {
            UniverseRules beta = CrearReglas("beta");
            UniverseRules alfa = CrearReglas("alfa");
            UniverseCatalog catalogo = Crear<UniverseCatalog>();
            catalogo.universes.Add(beta);
            catalogo.universes.Add(null);
            catalogo.universes.Add(alfa);

            List<UniverseRulesData> runtime = catalogo.BuildRuntimeRules();

            Assert.AreEqual(2, runtime.Count);
            Assert.AreEqual("alfa", runtime[0].id);
            Assert.AreEqual("beta", runtime[1].id);
            Assert.IsFalse(ReferenceEquals(runtime[0], alfa.Data));
            Assert.IsTrue(catalogo.Find("beta") == beta);
            Assert.IsTrue(catalogo.Find("gamma") == null);
        }

        // ---------------- Utilidades ----------------

        private T Crear<T>() where T : ScriptableObject
        {
            T instancia = ScriptableObject.CreateInstance<T>();
            _creados.Add(instancia);
            return instancia;
        }

        private UniverseRules CrearReglas(string id)
        {
            UniverseRules reglas = Crear<UniverseRules>();
            reglas.Initialize(TestRules.Make(id), new UniversePresentationDefaults());
            return reglas;
        }
    }
}
