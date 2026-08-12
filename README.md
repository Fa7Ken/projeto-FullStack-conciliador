# Robô Conciliador de Planilhas (Full Stack)

![Java](https://img.shields.io/badge/Java-21-orange?style=for-the-badge&logo=java)
![Spring Boot](https://img.shields.io/badge/Spring%20Boot-3.x-brightgreen?style=for-the-badge&logo=springboot)
![Angular](https://img.shields.io/badge/Angular-17%2B-red?style=for-the-badge&logo=angular)
![TypeScript](https://img.shields.io/badge/TypeScript-5.x-blue?style=for-the-badge&logo=typescript)

> Aplicação Web Full Stack desenvolvida para automação do processo de **Conciliação Financeira**. O sistema cruza dados de relatórios de vendas e extratos bancários em formato Excel (`.xlsx`), identificando divergências de valores e taxas automaticamente.

---

## O Problema de Negócio

No dia a dia financeiro, conferir manualmente centenas de transações de cartão e PIX contra os extratos bancários gera um gargalo operacional imenso e alta margem de erro. 

Este **Robô Conciliador** automatiza esse processo: lê os dois arquivos, aplica as regras de negócio para encontrar divergências de liquidação e exibe o resultado compilado na tela em poucos segundos.

---

## Tecnologias Utilizadas

### **Backend (`conciliador-api`)**
* **Java 21** - Linguagem principal.
* **Spring Boot 3** - Framework para construção da API REST.
* **Apache POI** - Biblioteca Java para manipulação e leitura de arquivos Excel (`.xlsx`).
* **Maven** - Gerenciador de dependências e build.

### **Frontend (`conciliador-web`)**
* **Angular** - Framework SPA para construção da interface web.
* **TypeScript** - Tipagem estática e segurança no código do cliente.
* **RxJS / HttpClient** - Comunicação assíncrona com a API Spring Boot via chamadas HTTP.
* **HTML5 / CSS3** - Interface responsiva com feedback visual dinâmico.

---

## Arquitetura e Fluxo de Dados

1. O usuário seleciona a modalidade (**Crédito** ou **Débito**) e faz o upload dos arquivos `.xlsx` (Vendas e Extrato).
2. O Frontend envia a requisição `POST` com os arquivos via `FormData` para o endpoint `/api/conciliacao/processar`.
3. O Backend Java recebe os Streams, executa o parsing via **Apache POI**, faz o cruzamento das chaves (Data + Bandeira + Valor) e calcula as diferenças.
4. O Spring Boot devolve um `JSON` com a lista de divergências.
5. O Angular renderiza a tabela interativa destacando os itens divergentes.

---

## Como Executar o Projeto Localmente

### **Pré-requisitos**
* **Java JDK 21** instalado.
* **Node.js (v18+)** e **npm** instalados.
* **Angular CLI** instalado (`npm install -g @angular/cli`).

---

### Executando o Backend (Java Spring Boot)

# Entre na pasta da API
cd conciliador-api

# Execute a aplicação via Maven (ou inicie pela sua IDE)
mvn spring-boot:run
A API estará rodando em: http://localhost:8080

---

### Executando o Backend (Java Spring Boot)

# Em outro terminal, entre na pasta do Frontend
cd conciliador-web

# Instale as dependências
npm install

# Suba o servidor de desenvolvimento
ng serve
Acesse no seu navegador: http://localhost:4200

Autor:
Desenvolvido por Fabricio do Nascimento
