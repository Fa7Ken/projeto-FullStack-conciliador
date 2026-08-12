package com.conciliador.service;

import com.conciliador.dto.ResultadoConciliacaoDTO;
import org.apache.poi.ss.usermodel.*;
import org.springframework.stereotype.Service;
import org.springframework.web.multipart.MultipartFile;

import java.io.InputStream;
import java.util.*;

@Service
public class ConciliacaoService {

    public List<ResultadoConciliacaoDTO> processarConciliacao(MultipartFile arquivoVendas, MultipartFile arquivoExtrato, String tipo) throws Exception {
        Map<String, Double> extratoAgrupado = mapearExtrato(arquivoExtrato, tipo);
        List<ResultadoConciliacaoDTO> resultados = new ArrayList<>();

        // Mapeamento das colunas baseadas em índice físico (L=11, M=12, S=18, Y=24)
        int idxTipo = 11, idxBandeira = 12, idxValor = 18, idxData = 24;
        String termoBusca = tipo.equalsIgnoreCase("CREDITO") ? "crédito" : "débito";

        try (InputStream is = arquivoVendas.getInputStream();
             Workbook workbook = WorkbookFactory.create(is)) {

            Sheet sheet = workbook.getSheetAt(0);
            boolean dentroTabela = false;

            for (Row row : sheet) {
                if (!dentroTabela) {
                    // Procura cabeçalho
                    for (Cell cell : row) {
                        String txt = cell.toString().toLowerCase();
                        if (txt.contains("bandeira") || txt.contains("tipo de cartão")) {
                            dentroTabela = true;
                            break;
                        }
                    }
                    continue;
                }

                Cell cellData = row.getCell(idxData);
                if (cellData == null || cellData.toString().trim().isEmpty() || cellData.toString().equals("-")) {
                    continue;
                }

                Cell cellTipo = row.getCell(idxTipo);
                if (cellTipo == null || !cellTipo.toString().toLowerCase().contains(termoBusca)) {
                    continue;
                }

                Cell cellBandeira = row.getCell(idxBandeira);
                String bandeira = (cellBandeira != null) ? cellBandeira.toString().trim().toUpperCase() : "OUTROS";

                Cell cellValor = row.getCell(idxValor);
                double valorVendido = converterParaNumero(cellValor);
                String dataStr = cellData.toString().trim();

                String chave = dataStr + "_" + tipo.toUpperCase() + "_" + bandeira;
                double valorExtrato = extratoAgrupado.getOrDefault(chave, 0.0);
                double diferenca = valorExtrato - valorVendido;

                resultados.add(new ResultadoConciliacaoDTO(dataStr, bandeira, valorVendido, valorExtrato, diferenca));
            }
        }

        return resultados;
    }

    private Map<String, Double> mapearExtrato(MultipartFile arquivoExtrato, String tipo) throws Exception {
        Map<String, Double> mapa = new HashMap<>();
        try (InputStream is = arquivoExtrato.getInputStream();
             Workbook workbook = WorkbookFactory.create(is)) {
            Sheet sheet = workbook.getSheetAt(0);

            for (Row row : sheet) {
                String linhaTexto = row.toString().toLowerCase();
                if (linhaTexto.contains("saldo") || !linhaTexto.contains("sicred")) continue;

                boolean ehDebito = linhaTexto.contains("deb") || linhaTexto.contains("déb");
                if (tipo.equalsIgnoreCase("DEBITO") && !ehDebito) continue;
                if (tipo.equalsIgnoreCase("CREDITO") && ehDebito) continue;

                String bandeira = "OUTROS";
                if (linhaTexto.contains("master")) bandeira = "MASTERCARD";
                else if (linhaTexto.contains("visa")) bandeira = "VISA";
                else if (linhaTexto.contains("elo")) bandeira = "ELO";

                Cell cellData = row.getCell(0); // Ajustar conforme coluna de data do extrato
                Cell cellValor = row.getCell(2); // Ajustar conforme coluna de valor do extrato

                if (cellData != null && cellValor != null) {
                    String key = cellData.toString().trim() + "_" + tipo.toUpperCase() + "_" + bandeira;
                    mapa.put(key, mapa.getOrDefault(key, 0.0) + converterParaNumero(cellValor));
                }
            }
        }
        return mapa;
    }

    private double converterParaNumero(Cell cell) {
        if (cell == null) return 0.0;
        if (cell.getCellType() == CellType.NUMERIC) return cell.getNumericCellValue();
        try {
            String str = cell.toString().replace("R$", "").trim().replace(".", "").replace(",", ".");
            return Double.parseDouble(str);
        } catch (Exception e) {
            return 0.0;
        }
    }
}