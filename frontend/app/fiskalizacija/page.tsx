"use client";

import { useState } from "react";
import Link from "next/link";
import {
  ArrowLeft,
  FileText,
  Receipt,
  Loader2,
  CheckCircle,
  Download,
  AlertCircle,
  Building2,
  User,
  CreditCard,
  FileCode2,
  Send,
  Copy,
  Check,
} from "lucide-react";

// ── Types ──

interface InvoiceParty {
  name: string;
  oib: string;
  address: string;
  city: string;
  postal_code: string;
  country_code: string;
  email?: string;
  iban?: string;
}

interface InvoiceLineItem {
  line_number: number;
  description: string;
  quantity: number;
  unit: string;
  unit_price: number;
  line_net_amount: number;
  pdv_rate: number;
}

interface InvoiceResult {
  id: string;
  invoice_number: string;
  issue_date: string;
  due_date: string;
  issuer: InvoiceParty;
  client: InvoiceParty;
  line_items: InvoiceLineItem[];
  net_amount: number;
  pdv_rate: number;
  pdv_amount: number;
  gross_amount: number;
  currency_code: string;
  module: string;
  analysis_id?: string;
  case_reference?: string;
  status: string;
  ubl_xml: string;
  notes?: string;
  disclaimer: string;
}

import { API_BASE } from "@/lib/api";
import { validateOib } from "@/lib/utils";
import { useAuth } from "@/lib/useAuth";

const MODULE_LABELS: Record<string, string> = {
  valuation: "Procjena nekretnine",
  contract: "Analiza ugovora",
  chronology: "Kronologija spisa",
  legal: "Pravna analiza",
};

const PDV_RATES = [
  { value: 25, label: "25% — Standardna stopa" },
  { value: 13, label: "13% — Snižena stopa" },
  { value: 5, label: "5% — Snižena stopa" },
  { value: 0, label: "0% — Oslobođeno PDV-a" },
];

// OIB validation now comes from lib/utils — it was copied here verbatim, and a
// third caller (registration) had meanwhile grown its own weaker version that
// only counted digits. One implementation means the invoice screen and the
// signup form cannot disagree about whether an OIB is real.

// ── Main Page ──

export default function FiskalizacijaPage() {
  const { authHeaders } = useAuth();
  // Form state
  const [module, setModule] = useState("legal");
  const [analysisId, setAnalysisId] = useState("");
  const [caseReference, setCaseReference] = useState("");
  const [serviceDescription, setServiceDescription] = useState("");
  const [netAmount, setNetAmount] = useState("");
  const [pdvRate, setPdvRate] = useState(25);
  const [paymentDueDays, setPaymentDueDays] = useState(15);
  const [notes, setNotes] = useState("");

  // Issuer
  const [issuerName, setIssuerName] = useState("");
  const [issuerOib, setIssuerOib] = useState("");
  const [issuerAddress, setIssuerAddress] = useState("");
  const [issuerCity, setIssuerCity] = useState("");
  const [issuerPostalCode, setIssuerPostalCode] = useState("");
  const [issuerEmail, setIssuerEmail] = useState("");
  const [issuerIban, setIssuerIban] = useState("");

  // Client
  const [clientName, setClientName] = useState("");
  const [clientOib, setClientOib] = useState("");
  const [clientAddress, setClientAddress] = useState("");
  const [clientCity, setClientCity] = useState("");
  const [clientPostalCode, setClientPostalCode] = useState("");
  const [clientEmail, setClientEmail] = useState("");

  // UI state
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<InvoiceResult | null>(null);
  const [showXml, setShowXml] = useState(false);
  const [copied, setCopied] = useState(false);

  // Computed
  const netNum = parseFloat(netAmount) || 0;
  const pdvAmount = Math.round(netNum * pdvRate) / 100;
  const grossAmount = netNum + pdvAmount;

  // OIB validation
  const issuerOibValid = issuerOib.length === 0 || validateOib(issuerOib);
  const clientOibValid = clientOib.length === 0 || validateOib(clientOib);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setResult(null);
    setLoading(true);

    try {
      const body = {
        module,
        analysis_id: analysisId || undefined,
        case_reference: caseReference || undefined,
        service_description: serviceDescription || undefined,
        net_amount: netNum,
        pdv_rate: pdvRate,
        payment_due_days: paymentDueDays,
        notes: notes || undefined,
        issuer: {
          name: issuerName,
          oib: issuerOib,
          address: issuerAddress,
          city: issuerCity,
          postal_code: issuerPostalCode,
          country_code: "HR",
          email: issuerEmail || undefined,
          iban: issuerIban || undefined,
        },
        client: {
          name: clientName,
          oib: clientOib,
          address: clientAddress,
          city: clientCity,
          postal_code: clientPostalCode,
          country_code: "HR",
          email: clientEmail || undefined,
        },
      };

      const res = await fetch(`${API_BASE}/api/invoice/generate`, {
        method: "POST",
        headers: { "Content-Type": "application/json", ...authHeaders },
        body: JSON.stringify(body),
      });

      if (!res.ok) {
        const err = await res.json().catch(() => ({}));
        throw new Error(err.error || `Greška ${res.status}`);
      }

      const data: InvoiceResult = await res.json();
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Nepoznata greška");
    } finally {
      setLoading(false);
    }
  };

  const downloadXml = () => {
    if (!result) return;
    const blob = new Blob([result.ubl_xml], { type: "application/xml" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `e-racun-${result.invoice_number}.xml`;
    a.click();
    URL.revokeObjectURL(url);
  };

  const copyXml = async () => {
    if (!result) return;
    await navigator.clipboard.writeText(result.ubl_xml);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-slate-50 to-white">
      {/* Header */}
      <header className="bg-white border-b border-slate-200 sticky top-0 z-40">
        <div className="max-w-5xl mx-auto px-4 sm:px-6 py-3 flex items-center gap-4">
          <Link
            href="/"
            className="flex items-center gap-2 text-slate-400 hover:text-slate-700 transition text-sm"
          >
            <ArrowLeft className="h-4 w-4" />
            <span className="hidden sm:inline">Početna</span>
          </Link>
          <div className="h-5 w-px bg-slate-200" />
          <div className="flex items-center gap-2">
            <div className="w-7 h-7 rounded-lg bg-emerald-100 flex items-center justify-center">
              <Receipt className="h-4 w-4 text-emerald-700" />
            </div>
            <div>
              <h1 className="text-sm font-bold text-slate-900">Fiskalizacija 2.0</h1>
              <p className="text-[10px] text-slate-400">e-Račun · UBL 2.1 · B2B</p>
            </div>
          </div>
        </div>
      </header>

      <main className="max-w-5xl mx-auto px-4 sm:px-6 py-6">
        {/* Info Banner */}
        <div className="mb-6 bg-emerald-50 border border-emerald-200 rounded-xl p-4">
          <div className="flex items-start gap-3">
            <FileCode2 className="h-5 w-5 text-emerald-600 mt-0.5 shrink-0" />
            <div className="text-sm text-emerald-800">
              <p className="font-semibold mb-1">Elektroničko izdavanje računa — obvezno od 01.01.2026.</p>
              <p className="text-emerald-700 text-xs leading-relaxed">
                Sukladno Zakonu o elektroničkom izdavanju računa, svi B2B računi moraju biti u
                UBL 2.1 formatu (EN 16931). PravoTek automatski generira e-račun povezan s
                analiziranim predmetom — samo unesite podatke stranaka i iznos.
              </p>
            </div>
          </div>
        </div>

        {/* Result Panel (shown after generation) */}
        {result && (
          <div className="mb-8 bg-white rounded-2xl border border-emerald-200 shadow-sm overflow-hidden">
            {/* Success Header */}
            <div className="bg-gradient-to-r from-emerald-600 to-emerald-700 px-6 py-4">
              <div className="flex items-center gap-3">
                <CheckCircle className="h-6 w-6 text-emerald-100" />
                <div>
                  <h2 className="text-white font-bold">e-Račun uspješno generiran</h2>
                  <p className="text-emerald-200 text-xs">
                    {result.invoice_number} · {result.issue_date} · UBL 2.1
                  </p>
                </div>
              </div>
            </div>

            {/* Invoice Summary */}
            <div className="p-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
                {/* Issuer */}
                <div className="bg-slate-50 rounded-xl p-4">
                  <div className="flex items-center gap-2 mb-3">
                    <Building2 className="h-4 w-4 text-slate-500" />
                    <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">Izdavatelj</span>
                  </div>
                  <p className="text-sm font-medium text-slate-900">{result.issuer.name}</p>
                  <p className="text-xs text-slate-500">OIB: {result.issuer.oib || "—"}</p>
                  <p className="text-xs text-slate-500">{result.issuer.address}, {result.issuer.city}</p>
                  {result.issuer.iban && <p className="text-xs text-slate-500">IBAN: {result.issuer.iban}</p>}
                </div>

                {/* Client */}
                <div className="bg-slate-50 rounded-xl p-4">
                  <div className="flex items-center gap-2 mb-3">
                    <User className="h-4 w-4 text-slate-500" />
                    <span className="text-xs font-semibold text-slate-800 uppercase tracking-wider">Primatelj</span>
                  </div>
                  <p className="text-sm font-medium text-slate-900">{result.client.name}</p>
                  <p className="text-xs text-slate-500">OIB: {result.client.oib || "—"}</p>
                  <p className="text-xs text-slate-500">{result.client.address}, {result.client.city}</p>
                </div>
              </div>

              {/* Amounts */}
              <div className="bg-gradient-to-r from-slate-50 to-slate-100 rounded-xl p-4 sm:p-5 mb-6">
                <div className="grid grid-cols-2 sm:grid-cols-4 gap-3 sm:gap-4 text-center">
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase tracking-wider mb-1">Neto</p>
                    <p className="text-lg font-bold text-slate-900">{result.net_amount.toLocaleString("hr-HR", { minimumFractionDigits: 2 })} €</p>
                  </div>
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase tracking-wider mb-1">PDV ({result.pdv_rate}%)</p>
                    <p className="text-lg font-bold text-slate-600">{result.pdv_amount.toLocaleString("hr-HR", { minimumFractionDigits: 2 })} €</p>
                  </div>
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase tracking-wider mb-1">Ukupno</p>
                    <p className="text-xl font-black text-emerald-700">{result.gross_amount.toLocaleString("hr-HR", { minimumFractionDigits: 2 })} €</p>
                  </div>
                  <div>
                    <p className="text-[10px] text-slate-500 uppercase tracking-wider mb-1">Rok plaćanja</p>
                    <p className="text-lg font-bold text-slate-900">{result.due_date}</p>
                  </div>
                </div>
              </div>

              {/* Line Items */}
              <div className="mb-6">
                <h3 className="text-xs font-semibold text-slate-800 uppercase tracking-wider mb-2">Stavke</h3>
                <div className="bg-white border border-slate-200 rounded-lg overflow-hidden">
                  <div className="grid grid-cols-[auto_1fr_auto] sm:grid-cols-[auto_1fr_auto_auto] gap-2 sm:gap-3 px-3 sm:px-4 py-2 bg-slate-50 text-[10px] text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-200">
                    <span>#</span>
                    <span>Opis usluge</span>
                    <span className="hidden sm:block">Kol.</span>
                    <span className="text-right">Iznos</span>
                  </div>
                  {result.line_items.map((item) => (
                    <div key={item.line_number} className="grid grid-cols-[auto_1fr_auto] sm:grid-cols-[auto_1fr_auto_auto] gap-2 sm:gap-3 px-3 sm:px-4 py-3 text-sm">
                      <span className="text-slate-400 font-mono">{item.line_number}.</span>
                      <span className="text-slate-800 break-words">{item.description}</span>
                      <span className="text-slate-500 hidden sm:block">{item.quantity}</span>
                      <span className="text-right font-medium text-slate-900">{item.line_net_amount.toLocaleString("hr-HR", { minimumFractionDigits: 2 })} €</span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Actions */}
              <div className="flex flex-wrap gap-2 sm:gap-3 mb-6">
                <button
                  onClick={downloadXml}
                  className="inline-flex items-center gap-2 bg-emerald-600 text-white text-sm font-medium px-5 py-2.5 rounded-lg hover:bg-emerald-700 transition"
                >
                  <Download className="h-4 w-4" />
                  Preuzmi UBL 2.1 XML
                </button>
                <button
                  onClick={copyXml}
                  className="inline-flex items-center gap-2 bg-slate-100 text-slate-700 text-sm font-medium px-5 py-2.5 rounded-lg hover:bg-slate-200 transition"
                >
                  {copied ? <Check className="h-4 w-4 text-emerald-600" /> : <Copy className="h-4 w-4" />}
                  {copied ? "Kopirano!" : "Kopiraj XML"}
                </button>
                <button
                  onClick={() => setShowXml(!showXml)}
                  className="inline-flex items-center gap-2 bg-slate-100 text-slate-700 text-sm font-medium px-5 py-2.5 rounded-lg hover:bg-slate-200 transition"
                >
                  <FileCode2 className="h-4 w-4" />
                  {showXml ? "Sakrij XML" : "Pregledaj XML"}
                </button>
                <button
                  onClick={() => alert("📤 e-Račun\n\nSlanje putem sustava e-Račun zahtijeva integraciju s FINA sustavom i aktivnu poslovnu karticu.")}
                  className="inline-flex items-center gap-2 bg-blue-600 text-white text-sm font-medium px-5 py-2.5 rounded-lg hover:bg-blue-700 transition"
                >
                  <Send className="h-4 w-4" />
                  Pošalji e-Račun
                </button>
              </div>

              {/* XML Preview */}
              {showXml && (
                <div className="mb-6">
                  <h3 className="text-xs font-semibold text-slate-800 uppercase tracking-wider mb-2">UBL 2.1 XML</h3>
                  <pre className="bg-slate-900 text-emerald-300 text-xs p-4 rounded-xl overflow-x-auto max-h-96 font-mono leading-relaxed">
                    {result.ubl_xml}
                  </pre>
                </div>
              )}

              {/* Disclaimer */}
              <p className="text-[11px] text-slate-400 italic">{result.disclaimer}</p>
            </div>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-6">
          {/* Module & Case Reference */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
            <h2 className="text-sm font-bold text-slate-900 mb-4 flex items-center gap-2">
              <FileText className="h-4 w-4 text-emerald-600" />
              Podaci o predmetu
            </h2>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Modul</label>
                <select
                  value={module}
                  onChange={(e) => setModule(e.target.value)}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                >
                  {Object.entries(MODULE_LABELS).map(([key, label]) => (
                    <option key={key} value={key}>{label}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">ID analize <span className="text-slate-400">(opcionalno)</span></label>
                <input
                  type="text"
                  value={analysisId}
                  onChange={(e) => setAnalysisId(e.target.value)}
                  placeholder="UUID analize"
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Broj predmeta <span className="text-slate-400">(opcionalno)</span></label>
                <input
                  type="text"
                  value={caseReference}
                  onChange={(e) => setCaseReference(e.target.value)}
                  placeholder="npr. Pn-1234/2026"
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                />
              </div>
            </div>
            <div className="mt-4">
              <label className="block text-xs font-medium text-slate-600 mb-1">Opis usluge <span className="text-slate-400">(opcionalno — automatski se popunjava prema modulu)</span></label>
              <input
                type="text"
                value={serviceDescription}
                onChange={(e) => setServiceDescription(e.target.value)}
                placeholder={MODULE_LABELS[module] ? `${MODULE_LABELS[module]} — analiza` : "Opis usluge"}
                className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
              />
            </div>
          </div>

          {/* Issuer & Client */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* Issuer */}
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
              <h2 className="text-sm font-bold text-slate-900 mb-4 flex items-center gap-2">
                <Building2 className="h-4 w-4 text-blue-600" />
                Izdavatelj (odvjetnik/ured)
              </h2>
              <div className="space-y-3">
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Naziv *</label>
                  <input
                    type="text"
                    required
                    value={issuerName}
                    onChange={(e) => setIssuerName(e.target.value)}
                    placeholder="Odvjetničko društvo Horvat d.o.o."
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">
                    OIB *
                    {issuerOib.length > 0 && !issuerOibValid && (
                      <span className="text-red-500 ml-1">— neispravan OIB</span>
                    )}
                    {issuerOib.length === 11 && issuerOibValid && (
                      <span className="text-emerald-600 ml-1">✓</span>
                    )}
                  </label>
                  <input
                    type="text"
                    required
                    maxLength={11}
                    value={issuerOib}
                    onChange={(e) => setIssuerOib(e.target.value.replace(/\D/g, ""))}
                    placeholder="12345678901"
                    className={`w-full border rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 outline-none ${
                      issuerOib.length > 0 && !issuerOibValid ? "border-red-300 bg-red-50" : "border-slate-300"
                    }`}
                  />
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-slate-600 mb-1">Adresa</label>
                    <input
                      type="text"
                      value={issuerAddress}
                      onChange={(e) => setIssuerAddress(e.target.value)}
                      placeholder="Ilica 1"
                      className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                    />
                  </div>
                  <div className="grid grid-cols-[1fr_80px] gap-2">
                    <div>
                      <label className="block text-xs font-medium text-slate-600 mb-1">Grad</label>
                      <input
                        type="text"
                        value={issuerCity}
                        onChange={(e) => setIssuerCity(e.target.value)}
                        placeholder="Zagreb"
                        className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-medium text-slate-600 mb-1">PBR</label>
                      <input
                        type="text"
                        value={issuerPostalCode}
                        onChange={(e) => setIssuerPostalCode(e.target.value)}
                        placeholder="10000"
                        className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                      />
                    </div>
                  </div>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Email</label>
                  <input
                    type="email"
                    value={issuerEmail}
                    onChange={(e) => setIssuerEmail(e.target.value)}
                    placeholder="ured@odvjetnik-horvat.hr"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">IBAN</label>
                  <input
                    type="text"
                    value={issuerIban}
                    onChange={(e) => setIssuerIban(e.target.value)}
                    placeholder="HR1234567890123456789"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                  />
                </div>
              </div>
            </div>

            {/* Client */}
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
              <h2 className="text-sm font-bold text-slate-900 mb-4 flex items-center gap-2">
                <User className="h-4 w-4 text-violet-600" />
                Primatelj (klijent)
              </h2>
              <div className="space-y-3">
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Naziv *</label>
                  <input
                    type="text"
                    required
                    value={clientName}
                    onChange={(e) => setClientName(e.target.value)}
                    placeholder="Tvrtka d.o.o. / Ivan Ivić"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">
                    OIB
                    {clientOib.length > 0 && !clientOibValid && (
                      <span className="text-red-500 ml-1">— neispravan OIB</span>
                    )}
                    {clientOib.length === 11 && clientOibValid && (
                      <span className="text-emerald-600 ml-1">✓</span>
                    )}
                  </label>
                  <input
                    type="text"
                    maxLength={11}
                    value={clientOib}
                    onChange={(e) => setClientOib(e.target.value.replace(/\D/g, ""))}
                    placeholder="98765432109"
                    className={`w-full border rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 outline-none ${
                      clientOib.length > 0 && !clientOibValid ? "border-red-300 bg-red-50" : "border-slate-300"
                    }`}
                  />
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-slate-600 mb-1">Adresa</label>
                    <input
                      type="text"
                      value={clientAddress}
                      onChange={(e) => setClientAddress(e.target.value)}
                      placeholder="Vukovarska 78"
                      className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                    />
                  </div>
                  <div className="grid grid-cols-[1fr_80px] gap-2">
                    <div>
                      <label className="block text-xs font-medium text-slate-600 mb-1">Grad</label>
                      <input
                        type="text"
                        value={clientCity}
                        onChange={(e) => setClientCity(e.target.value)}
                        placeholder="Split"
                        className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-medium text-slate-600 mb-1">PBR</label>
                      <input
                        type="text"
                        value={clientPostalCode}
                        onChange={(e) => setClientPostalCode(e.target.value)}
                        placeholder="21000"
                        className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                      />
                    </div>
                  </div>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-600 mb-1">Email</label>
                  <input
                    type="email"
                    value={clientEmail}
                    onChange={(e) => setClientEmail(e.target.value)}
                    placeholder="klijent@tvrtka.hr"
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Financial Details */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
            <h2 className="text-sm font-bold text-slate-900 mb-4 flex items-center gap-2">
              <CreditCard className="h-4 w-4 text-amber-600" />
              Financijski podaci
            </h2>
            <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4">
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Neto iznos (EUR) *</label>
                <input
                  type="number"
                  required
                  min="0.01"
                  step="0.01"
                  value={netAmount}
                  onChange={(e) => setNetAmount(e.target.value)}
                  placeholder="500.00"
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Stopa PDV-a</label>
                <select
                  value={pdvRate}
                  onChange={(e) => setPdvRate(parseInt(e.target.value))}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                >
                  {PDV_RATES.map((r) => (
                    <option key={r.value} value={r.value}>{r.label}</option>
                  ))}
                </select>
              </div>
              <div>
                <label className="block text-xs font-medium text-slate-600 mb-1">Rok plaćanja (dana)</label>
                <input
                  type="number"
                  min={1}
                  max={365}
                  value={paymentDueDays}
                  onChange={(e) => setPaymentDueDays(parseInt(e.target.value) || 15)}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none"
                />
              </div>
              <div className="bg-emerald-50 rounded-lg p-3 flex flex-col justify-center">
                <p className="text-[10px] text-emerald-500 uppercase tracking-wider font-semibold">Ukupno s PDV-om</p>
                <p className="text-xl font-black text-emerald-700">
                  {grossAmount.toLocaleString("hr-HR", { minimumFractionDigits: 2 })} €
                </p>
                <p className="text-[10px] text-emerald-600">
                  PDV: {pdvAmount.toLocaleString("hr-HR", { minimumFractionDigits: 2 })} €
                </p>
              </div>
            </div>
            <div className="mt-4">
              <label className="block text-xs font-medium text-slate-600 mb-1">Napomena <span className="text-slate-400">(opcionalno)</span></label>
              <textarea
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
                rows={2}
                placeholder="Dodatne napomene koje će se pojaviti na računu..."
                className="w-full border border-slate-300 rounded-lg px-3 py-2 text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 outline-none resize-none"
              />
            </div>
          </div>

          {/* Error */}
          {error && (
            <div className="bg-red-50 border border-red-200 rounded-xl p-4 flex items-start gap-3">
              <AlertCircle className="h-5 w-5 text-red-500 shrink-0 mt-0.5" />
              <div>
                <p className="text-sm font-medium text-red-800">Greška pri generiranju</p>
                <p className="text-xs text-red-600 mt-0.5">{error}</p>
              </div>
            </div>
          )}

          {/* Submit */}
          <div className="flex flex-col items-end gap-2">
            <button
              type="submit"
              disabled={loading || netNum <= 0 || !issuerName || !clientName || (issuerOib.length > 0 && !issuerOibValid) || (clientOib.length > 0 && !clientOibValid)}
              className="inline-flex items-center gap-2 bg-emerald-600 text-white text-sm font-semibold px-8 py-3 rounded-xl hover:bg-emerald-700 disabled:opacity-50 disabled:cursor-not-allowed transition shadow-sm"
            >
              {loading ? (
                <>
                  <Loader2 className="h-4 w-4 animate-spin" />
                  Generiranje...
                </>
              ) : (
                <>
                  <Receipt className="h-4 w-4" />
                  Generiraj e-Račun
                </>
              )}
            </button>
          </div>
        </form>
      </main>

      {/* Footer */}
      <footer className="max-w-5xl mx-auto px-4 sm:px-6 pb-6">
        <div className="border-t border-slate-200 pt-4 text-center space-y-1 text-[11px] text-slate-400">
          <div className="flex flex-col sm:flex-row items-center justify-between gap-1">
            <span>PravoTek v5.0 · Fiskalizacija 2.0</span>
            <span className="text-center">
              e-Račun sukladan EN 16931 / UBL 2.1 — Zakon o elektroničkom izdavanju računa
            </span>
          </div>
          <p>Ovaj alat ne zamjenjuje računovodstveni softver. Korisnik je odgovoran za provjeru točnosti podataka prije slanja e-računa.</p>
        </div>
      </footer>
    </div>
  );
}
