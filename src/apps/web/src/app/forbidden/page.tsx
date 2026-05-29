export default function ForbiddenPage() {
  return (
    <main className="forbidden-page">
      <section className="panel forbidden-card stack-gap">
        <p className="eyebrow">Forbidden</p>
        <h1>Role cannot enter this surface.</h1>
        <p>
          Web shell only admits routes allowed for `Administrator` or `Operator`. Return to login
          or switch to route your role owns.
        </p>
        <a className="primary-button" href="/login">
          Back to login
        </a>
      </section>
    </main>
  );
}
