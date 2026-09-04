// spec 031 (US5): flujo colaborativo go/no-go
export interface LicitacionInteres {
  id: number;
  licitacionId: number;
  workspaceId: number | null;
  conversacionId: number | null;
  marcadoPor: string;
  estadoLicitacionAlMarcar: number;
  estadoLicitacionActual: number;
  createdAt: string;
  updatedAt: string;
  estadoCambio: boolean;
}

export interface LicitacionInteresListItem extends LicitacionInteres {
  licitacionNombre: string;
  codigoExterno?: string;
}
