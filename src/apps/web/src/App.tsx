import { BrowserRouter, Routes, Route } from "react-router-dom";
import { AuthProvider } from "@/context/auth-context";
import LandingPage from "@/pages/LandingPage";
import DashboardRouter from "@/pages/DashboardRouter";
import AdministratorPage from "@/pages/AdministratorPage";
import OperatorPage from "@/pages/OperatorPage";
import ForbiddenPage from "@/pages/ForbiddenPage";

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/dashboard" element={<DashboardRouter />} />
          <Route path="/administrator" element={<AdministratorPage />} />
          <Route path="/operator" element={<OperatorPage />} />
          <Route path="/forbidden" element={<ForbiddenPage />} />
          <Route path="*" element={<LandingPage />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
