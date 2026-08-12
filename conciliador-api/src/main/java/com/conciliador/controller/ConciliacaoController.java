package com.conciliador.controller;

import com.conciliador.dto.ResultadoConciliacaoDTO;
import com.conciliador.service.ConciliacaoService;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.multipart.MultipartFile;

import java.util.List;

@RestController
@RequestMapping("/api/conciliacao")
@CrossOrigin(origins = "*", allowedHeaders = "*") // Libera acesso total para desenvolvimento local
public class ConciliacaoController {

    @Autowired
    private ConciliacaoService service;

    @PostMapping("/processar")
    public ResponseEntity<?> processar(
            @RequestParam("vendas") MultipartFile arquivoVendas,
            @RequestParam("extrato") MultipartFile arquivoExtrato,
            @RequestParam("tipo") String tipo) {
        try {
            List<ResultadoConciliacaoDTO> resultado = service.processarConciliacao(arquivoVendas, arquivoExtrato, tipo);
            return ResponseEntity.ok(resultado);
        } catch (Exception e) {
            return ResponseEntity.badRequest().body("Erro ao processar: " + e.getMessage());
        }
    }
}