import { useState } from "react";
import Layout from "../../components/Layout";
import SeccionDocumentosRevisor from "./SeccionDocumentosRevisor";

const SECCIONES = [
  { id: "pendientes", label: "Pendientes", icono: "📋" },
];

export default function DashboardRevisor() {
  const [seccion, setSeccion] = useState("pendientes");

  return (
    <Layout
      titulo="QualityDoc"
      secciones={SECCIONES}
      seccionActiva={seccion}
      setSeccion={setSeccion}
    >
      <SeccionDocumentosRevisor />
    </Layout>
  );
}